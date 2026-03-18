namespace ClaudeMaximus.Models;

/// <summary>
/// Represents a Claude CLI profile with separate authentication context.
/// The ProfileId is passed as --profile flag to the CLI.
/// </summary>
/// <remarks>Created by Claude</remarks>
public sealed class ClaudeProfileModel
{
	/// <summary>Profile identifier passed to --profile CLI flag.</summary>
	public string ProfileId { get; set; } = string.Empty;

	/// <summary>Display name shown in the dropdown (typically the account email).</summary>
	public string DisplayName { get; set; } = string.Empty;
}
