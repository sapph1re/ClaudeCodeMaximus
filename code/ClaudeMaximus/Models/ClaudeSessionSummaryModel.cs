using System;

namespace ClaudeMaximus.Models;

/// <remarks>Created by Claude</remarks>
public sealed class ClaudeSessionSummaryModel
{
	public required string SessionId { get; init; }
	public required string JsonlPath { get; init; }
	public DateTimeOffset Created { get; init; }
	public DateTimeOffset LastUsed { get; init; }
	public int MessageCount { get; init; }
	public string? FirstUserPrompt { get; init; }

	/// <summary>Claude-generated title, populated asynchronously. Null until generated.</summary>
	public string? GeneratedTitle { get; set; }
}
