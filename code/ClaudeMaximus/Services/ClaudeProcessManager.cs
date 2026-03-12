using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClaudeMaximus.Models;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public sealed class ClaudeProcessManager : IClaudeProcessManager
{
	public async Task SendMessageAsync(
		string workingDirectory,
		string claudePath,
		string? sessionId,
		string userMessage,
		Action<ClaudeStreamEvent> onEvent,
		CancellationToken cancellationToken = default)
	{
		var args = BuildArguments(sessionId);

		var psi = new ProcessStartInfo(claudePath, args)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardInput  = true,
			RedirectStandardOutput = true,
			RedirectStandardError  = true,
			UseShellExecute  = false,
			CreateNoWindow   = true,
			StandardInputEncoding  = Encoding.UTF8,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding  = Encoding.UTF8,
		};

		Process process;
		try
		{
			process = Process.Start(psi)
				?? throw new InvalidOperationException("Failed to start claude process.");
		}
		catch (Win32Exception ex)
		{
			onEvent(new ClaudeStreamEvent
			{
				Type     = "system",
				Subtype  = "error",
				Content  = $"Could not launch claude: {ex.Message}. Check the claude path in Settings.",
				IsError  = true,
			});
			return;
		}

		using (process)
		{
			await process.StandardInput.WriteLineAsync(userMessage);
			process.StandardInput.Close();

			string? line;
			while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) != null)
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var evt = TryParseEvent(line);
				if (evt != null)
					onEvent(evt);
			}

			await process.WaitForExitAsync(cancellationToken);

			if (process.ExitCode != 0)
			{
				var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
				if (!string.IsNullOrWhiteSpace(stderr))
				{
					onEvent(new ClaudeStreamEvent
					{
						Type    = "system",
						Subtype = "error",
						Content = $"claude exited with code {process.ExitCode}: {stderr.Trim()}",
						IsError = true,
					});
				}
			}
		}
	}

	private static string BuildArguments(string? sessionId)
	{
		var args = "--output-format stream-json";
		if (!string.IsNullOrEmpty(sessionId))
			args += $" --resume {sessionId}";
		return args;
	}

	private static ClaudeStreamEvent? TryParseEvent(string line)
	{
		try
		{
			using var doc = JsonDocument.Parse(line);
			var root = doc.RootElement;

			if (!root.TryGetProperty("type", out var typeEl))
				return null;

			var type    = typeEl.GetString() ?? string.Empty;
			var subtype = root.TryGetProperty("subtype", out var subEl) ? subEl.GetString() : null;

			return type switch
			{
				"assistant" => ParseAssistantEvent(root, type, subtype),
				"system"    => ParseSystemEvent(root, type, subtype),
				"result"    => ParseResultEvent(root, type, subtype),
				_           => null,
			};
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static ClaudeStreamEvent? ParseAssistantEvent(JsonElement root, string type, string? subtype)
	{
		if (!root.TryGetProperty("message", out var msg))
			return null;

		var content = ExtractTextContent(msg);
		if (string.IsNullOrEmpty(content))
			return null;

		return new ClaudeStreamEvent { Type = type, Subtype = subtype, Content = content };
	}

	private static ClaudeStreamEvent ParseSystemEvent(JsonElement root, string type, string? subtype)
	{
		var content = root.TryGetProperty("summary", out var summary)
			? summary.GetString()
			: root.TryGetProperty("message", out var msg) ? msg.GetString() : null;

		return new ClaudeStreamEvent { Type = type, Subtype = subtype, Content = content };
	}

	private static ClaudeStreamEvent ParseResultEvent(JsonElement root, string type, string? subtype)
	{
		var sessionId = root.TryGetProperty("session_id", out var sidEl) ? sidEl.GetString() : null;
		var isError   = root.TryGetProperty("is_error", out var errEl) && errEl.GetBoolean();
		var errorMsg  = isError && root.TryGetProperty("error", out var errMsgEl)
			? errMsgEl.GetString()
			: null;

		return new ClaudeStreamEvent
		{
			Type      = type,
			Subtype   = subtype,
			SessionId = sessionId,
			IsError   = isError,
			Content   = errorMsg,
		};
	}

	private static string? ExtractTextContent(JsonElement messageElement)
	{
		if (!messageElement.TryGetProperty("content", out var contentArray))
			return null;

		var sb = new StringBuilder();
		foreach (var block in contentArray.EnumerateArray())
		{
			if (!block.TryGetProperty("type", out var blockType))
				continue;

			switch (blockType.GetString())
			{
				case "text" when block.TryGetProperty("text", out var text):
					sb.Append(text.GetString());
					break;
				case "tool_use" when block.TryGetProperty("name", out var name):
					sb.AppendLine();
					sb.AppendLine($"[Tool: {name.GetString()}]");
					break;
			}
		}

		return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
	}
}
