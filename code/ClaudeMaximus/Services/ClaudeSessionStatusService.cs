using System;
using System.IO;
using Serilog;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public sealed class ClaudeSessionStatusService : IClaudeSessionStatusService
{
	private static readonly ILogger _log = Log.ForContext<ClaudeSessionStatusService>();

	public bool IsSessionResumable(string workingDirectory, string claudeSessionId)
	{
		if (string.IsNullOrEmpty(workingDirectory) || string.IsNullOrEmpty(claudeSessionId))
			return false;

		try
		{
			var slug = BuildProjectSlug(workingDirectory);
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var sessionPath = Path.Combine(
				userProfile,
				Constants.ClaudeSessions.ClaudeHomeFolderName,
				Constants.ClaudeSessions.ProjectsFolderName,
				slug,
				claudeSessionId + Constants.ClaudeSessions.SessionFileExtension);

			var exists = File.Exists(sessionPath);
			_log.Debug("Resumability check: {SessionId} slug={Slug} path={Path} exists={Exists}",
				claudeSessionId, slug, sessionPath, exists);
			return exists;
		}
		catch (Exception ex)
		{
			_log.Warning(ex, "Error checking session resumability for {SessionId}", claudeSessionId);
			return false;
		}
	}

	/// <summary>
	/// Derives the project slug that Claude Code uses for its session storage path.
	/// Replaces ':', '\', '/' with '-'. Trailing separators are trimmed first.
	/// Example: C:\Projects\Foo → C--Projects-Foo
	/// </summary>
	private static string BuildProjectSlug(string workingDirectory)
	{
		return workingDirectory
			.TrimEnd('\\', '/')
			.Replace(':', '-')
			.Replace('\\', '-')
			.Replace('/', '-');
	}
}
