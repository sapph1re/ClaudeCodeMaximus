using System.Collections.Generic;
using ClaudeMaximus.Models;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public interface IClaudeSessionImportService
{
	/// <summary>
	/// Scans ~/.claude/projects/&lt;slug&gt;/ for JSONL session files and returns
	/// summary metadata for each discovered session (FR.13.2, FR.13.3).
	/// </summary>
	IReadOnlyList<ClaudeSessionSummaryModel> DiscoverSessions(string workingDirectory);

	/// <summary>
	/// Parses a Claude Code JSONL session file into ClaudeMaximus session entries (FR.13.7).
	/// Extracts USER, ASSISTANT, and tool-use SYSTEM entries. Skips malformed lines.
	/// </summary>
	IReadOnlyList<SessionEntryModel> ParseJsonlSession(string jsonlPath);
}
