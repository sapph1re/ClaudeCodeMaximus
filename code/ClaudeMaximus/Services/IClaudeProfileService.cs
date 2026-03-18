using System.Threading.Tasks;

namespace ClaudeMaximus.Services;

/// <summary>
/// Queries Claude CLI profile authentication status and launches interactive auth.
/// </summary>
/// <remarks>Created by Claude</remarks>
public interface IClaudeProfileService
{
	/// <summary>
	/// Runs <c>claude auth status [--profile id]</c> and returns the account email,
	/// or null if not authenticated or the command fails.
	/// </summary>
	Task<string?> GetAccountEmailAsync(string claudePath, string? profileId);

	/// <summary>
	/// Launches <c>claude auth login --profile id</c> in a visible console window
	/// and waits for it to exit. Returns true if the process exited with code 0.
	/// </summary>
	Task<bool> LaunchAuthLoginAsync(string claudePath, string profileId);
}
