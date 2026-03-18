using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using ClaudeMaximus.Models;
using Serilog;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public sealed class ClaudeSessionImportService : IClaudeSessionImportService
{
	private static readonly ILogger _log = Log.ForContext<ClaudeSessionImportService>();

	public IReadOnlyList<ClaudeSessionSummaryModel> DiscoverSessions(string workingDirectory)
	{
		var slug = Constants.ClaudeSessions.BuildProjectSlug(workingDirectory);
		var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var slugDir = Path.Combine(
			userProfile,
			Constants.ClaudeSessions.ClaudeHomeFolderName,
			Constants.ClaudeSessions.ProjectsFolderName,
			slug);

		if (!Directory.Exists(slugDir))
		{
			_log.Information("Import: no slug directory found at {SlugDir}", slugDir);
			return [];
		}

		var results = new List<ClaudeSessionSummaryModel>();

		foreach (var jsonlPath in Directory.GetFiles(slugDir, "*" + Constants.ClaudeSessions.SessionFileExtension))
		{
			var sessionId = Path.GetFileNameWithoutExtension(jsonlPath);
			if (string.IsNullOrEmpty(sessionId))
				continue;

			try
			{
				var summary = ExtractSummary(sessionId, jsonlPath);
				if (summary != null)
					results.Add(summary);
			}
			catch (Exception ex)
			{
				_log.Warning(ex, "Import: failed to extract summary from {Path}", jsonlPath);
			}
		}

		results.Sort((a, b) => b.LastUsed.CompareTo(a.LastUsed));
		_log.Information("Import: discovered {Count} sessions for slug {Slug}", results.Count, slug);
		return results;
	}

	public IReadOnlyList<SessionEntryModel> ParseJsonlSession(string jsonlPath)
	{
		var entries = new List<SessionEntryModel>();

		using var stream = new FileStream(jsonlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream, Encoding.UTF8);

		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			if (string.IsNullOrWhiteSpace(line))
				continue;

			try
			{
				using var doc = JsonDocument.Parse(line);
				var root = doc.RootElement;

				if (!root.TryGetProperty("type", out var typeEl))
					continue;

				var type = typeEl.GetString();
				var timestamp = ParseTimestamp(root);

				switch (type)
				{
					case "user":
						ParseUserEvent(root, timestamp, entries);
						break;
					case "assistant":
						ParseAssistantEvent(root, timestamp, entries);
						break;
				}
			}
			catch (JsonException ex)
			{
				_log.Debug("Import: skipping malformed JSONL line: {Error}", ex.Message);
			}
			catch (Exception ex)
			{
				_log.Debug("Import: skipping line due to error: {Error}", ex.Message);
			}
		}

		_log.Information("Import: parsed {Count} entries from {Path}", entries.Count, jsonlPath);
		return entries;
	}

	private static ClaudeSessionSummaryModel? ExtractSummary(string sessionId, string jsonlPath)
	{
		DateTimeOffset? firstTimestamp = null;
		DateTimeOffset? lastTimestamp = null;
		string? firstUserPrompt = null;
		var messageCount = 0;

		using var stream = new FileStream(jsonlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream, Encoding.UTF8);

		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			if (string.IsNullOrWhiteSpace(line))
				continue;

			try
			{
				using var doc = JsonDocument.Parse(line);
				var root = doc.RootElement;

				if (!root.TryGetProperty("type", out var typeEl))
					continue;

				var type = typeEl.GetString();
				var timestamp = ParseTimestamp(root);

				firstTimestamp ??= timestamp;
				lastTimestamp = timestamp;

				if (type is "user" or "assistant")
					messageCount++;

				if (type == "user" && firstUserPrompt == null)
					firstUserPrompt = ExtractUserContent(root, Constants.ClaudeSessions.FirstPromptMaxLength);
			}
			catch (JsonException)
			{
				// Skip malformed lines during discovery
			}
		}

		if (messageCount == 0)
		{
			return new ClaudeSessionSummaryModel
			{
				SessionId = sessionId,
				JsonlPath = jsonlPath,
				Created = firstTimestamp ?? DateTimeOffset.MinValue,
				LastUsed = lastTimestamp ?? DateTimeOffset.MinValue,
				MessageCount = 0,
				FirstUserPrompt = null,
			};
		}

		return new ClaudeSessionSummaryModel
		{
			SessionId = sessionId,
			JsonlPath = jsonlPath,
			Created = firstTimestamp ?? DateTimeOffset.MinValue,
			LastUsed = lastTimestamp ?? DateTimeOffset.MinValue,
			MessageCount = messageCount,
			FirstUserPrompt = firstUserPrompt,
		};
	}

	private static void ParseUserEvent(JsonElement root, DateTimeOffset timestamp, List<SessionEntryModel> entries)
	{
		var content = ExtractUserContent(root, maxLength: 0);
		if (string.IsNullOrWhiteSpace(content))
			return;

		entries.Add(new SessionEntryModel
		{
			Timestamp = timestamp,
			Role = Constants.SessionFile.RoleUser,
			Content = content,
		});
	}

	private static void ParseAssistantEvent(JsonElement root, DateTimeOffset timestamp, List<SessionEntryModel> entries)
	{
		if (!root.TryGetProperty("message", out var msg))
			return;
		if (!msg.TryGetProperty("content", out var contentArray))
			return;
		if (contentArray.ValueKind != JsonValueKind.Array)
			return;

		var textBuilder = new StringBuilder();
		var toolSummaries = new List<string>();

		foreach (var block in contentArray.EnumerateArray())
		{
			if (!block.TryGetProperty("type", out var blockType))
				continue;

			var blockTypeStr = blockType.GetString();

			if (blockTypeStr == "text" && block.TryGetProperty("text", out var text))
			{
				textBuilder.Append(text.GetString());
			}
			else if (blockTypeStr == "tool_use")
			{
				var toolName = block.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Unknown";
				var description = ExtractToolDescription(block);
				toolSummaries.Add(string.IsNullOrEmpty(description)
					? $"[Tool: {toolName}]"
					: $"[Tool: {toolName}] {description}");
			}
		}

		// Add text content as ASSISTANT entry
		var textContent = textBuilder.ToString().TrimEnd();
		if (!string.IsNullOrWhiteSpace(textContent))
		{
			entries.Add(new SessionEntryModel
			{
				Timestamp = timestamp,
				Role = Constants.SessionFile.RoleAssistant,
				Content = textContent,
			});
		}

		// Add tool use summaries as SYSTEM entries
		foreach (var summary in toolSummaries)
		{
			entries.Add(new SessionEntryModel
			{
				Timestamp = timestamp,
				Role = Constants.SessionFile.RoleSystem,
				Content = summary,
			});
		}
	}

	/// <summary>
	/// Extracts user message content. The user event has message.content as a plain string.
	/// </summary>
	private static string? ExtractUserContent(JsonElement root, int maxLength)
	{
		if (!root.TryGetProperty("message", out var msg))
			return null;
		if (!msg.TryGetProperty("content", out var contentEl))
			return null;

		string? content = null;

		if (contentEl.ValueKind == JsonValueKind.String)
			content = contentEl.GetString();
		else if (contentEl.ValueKind == JsonValueKind.Array)
		{
			// Fallback: some versions may use array format
			var sb = new StringBuilder();
			foreach (var block in contentEl.EnumerateArray())
			{
				if (block.TryGetProperty("type", out var bt) && bt.GetString() == "text"
					&& block.TryGetProperty("text", out var text))
					sb.Append(text.GetString());
			}
			if (sb.Length > 0)
				content = sb.ToString();
		}

		if (content == null)
			return null;

		if (maxLength > 0 && content.Length > maxLength)
			return content[..maxLength];

		return content;
	}

	/// <summary>
	/// Extracts a brief description from a tool_use block's input.
	/// Prefers 'description' field, falls back to 'command' (truncated).
	/// </summary>
	private static string? ExtractToolDescription(JsonElement block)
	{
		if (!block.TryGetProperty("input", out var input))
			return null;
		if (input.ValueKind != JsonValueKind.Object)
			return null;

		if (input.TryGetProperty("description", out var desc))
			return desc.GetString();

		if (input.TryGetProperty("command", out var cmd))
		{
			var cmdStr = cmd.GetString();
			if (cmdStr != null && cmdStr.Length > 80)
				return cmdStr[..80] + "...";
			return cmdStr;
		}

		if (input.TryGetProperty("file_path", out var fp))
			return fp.GetString();

		return null;
	}

	private static DateTimeOffset ParseTimestamp(JsonElement root)
	{
		if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
		{
			if (DateTimeOffset.TryParse(ts.GetString(), out var result))
				return result;
		}
		return DateTimeOffset.UtcNow;
	}
}
