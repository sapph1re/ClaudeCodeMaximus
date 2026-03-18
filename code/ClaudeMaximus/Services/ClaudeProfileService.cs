using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public sealed class ClaudeProfileService : IClaudeProfileService
{
	private static readonly ILogger _log = Log.ForContext<ClaudeProfileService>();

	public async Task<string?> GetAccountEmailAsync(string claudePath, string? profileId)
	{
		var args = "auth status";
		if (!string.IsNullOrEmpty(profileId))
			args += $" --profile {profileId}";

		var output = await RunClaudeCommandAsync(claudePath, args);
		if (string.IsNullOrWhiteSpace(output))
			return null;

		try
		{
			using var doc = JsonDocument.Parse(output);
			var root = doc.RootElement;

			if (root.TryGetProperty("loggedIn", out var loggedIn) && loggedIn.GetBoolean()
			    && root.TryGetProperty("email", out var email))
				return email.GetString();
		}
		catch (JsonException ex)
		{
			_log.Warning(ex, "Failed to parse claude auth status output for profile {ProfileId}", profileId);
		}

		return null;
	}

	public async Task<bool> LaunchAuthLoginAsync(string claudePath, string profileId)
	{
		var args = $"auth login --profile {profileId}";

		_log.Information("Launching interactive auth login for profile {ProfileId}", profileId);

		Process? process = TryStartVisibleProcess(claudePath, args);

		// On Windows, 'claude' is often a .cmd file; retry via cmd.exe
		if (process == null && OperatingSystem.IsWindows())
		{
			var cmdArgs = $"/c \"{claudePath}\" {args}";
			process = TryStartVisibleProcess("cmd.exe", cmdArgs);
		}

		if (process == null)
		{
			_log.Error("Failed to start claude auth login for profile {ProfileId}", profileId);
			return false;
		}

		using (process)
		{
			await process.WaitForExitAsync();
			var exitCode = process.ExitCode;
			_log.Information("Auth login for profile {ProfileId} exited with code {ExitCode}", profileId, exitCode);
			return exitCode == 0;
		}
	}

	private async Task<string?> RunClaudeCommandAsync(string claudePath, string args)
	{
		Process? process = TryStartHiddenProcess(claudePath, args);

		if (process == null && OperatingSystem.IsWindows())
		{
			var cmdArgs = $"/c \"{claudePath}\" {args}";
			process = TryStartHiddenProcess("cmd.exe", cmdArgs);
		}

		if (process == null)
			return null;

		using (process)
		{
			var output = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
			return output;
		}
	}

	private static Process? TryStartHiddenProcess(string fileName, string arguments)
	{
		var psi = new ProcessStartInfo(fileName, arguments)
		{
			RedirectStandardOutput = true,
			RedirectStandardError  = true,
			UseShellExecute        = false,
			CreateNoWindow         = true,
			StandardOutputEncoding = Encoding.UTF8,
		};

		try
		{
			return Process.Start(psi);
		}
		catch (Win32Exception ex)
		{
			_log.Warning("Failed to start {FileName}: {Message}", fileName, ex.Message);
			return null;
		}
	}

	private static Process? TryStartVisibleProcess(string fileName, string arguments)
	{
		var psi = new ProcessStartInfo(fileName, arguments)
		{
			UseShellExecute = true,
			CreateNoWindow  = false,
		};

		try
		{
			return Process.Start(psi);
		}
		catch (Win32Exception ex)
		{
			_log.Warning("Failed to start visible {FileName}: {Message}", fileName, ex.Message);
			return null;
		}
	}
}
