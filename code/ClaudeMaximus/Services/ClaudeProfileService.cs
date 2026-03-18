using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public sealed class ClaudeProfileService : IClaudeProfileService
{
	private static readonly ILogger _log = Log.ForContext<ClaudeProfileService>();

	public string ProfilesRootDirectory { get; }

	public ClaudeProfileService()
	{
		var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		ProfilesRootDirectory = Path.Combine(appData, Constants.AppDataFolderName, Constants.ProfilesFolderName);
	}

	public async Task<string?> GetAccountEmailAsync(string claudePath, string? configDir)
	{
		var output = await RunClaudeCommandAsync(claudePath, "auth status", configDir);
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
			_log.Warning(ex, "Failed to parse claude auth status output for configDir {ConfigDir}", configDir);
		}

		return null;
	}

	public async Task LaunchAuthLoginAsync(string claudePath, string configDir)
	{
		_log.Information("Launching interactive auth login with configDir {ConfigDir}", configDir);

		// Ensure the config directory exists
		Directory.CreateDirectory(configDir);

		Process? process;

		if (OperatingSystem.IsWindows())
		{
			// On Windows, 'claude' is typically a .cmd file. Launching it with
			// UseShellExecute=true causes the .cmd wrapper to exit immediately
			// after spawning the node process, so WaitForExitAsync returns before
			// the user can complete browser-based auth. Using cmd.exe /c with
			// & pause keeps the window open until auth finishes and the user
			// presses a key.
			var cmdArgs = $"/c \"set CLAUDE_CONFIG_DIR={configDir} && \"{claudePath}\" auth login & pause\"";
			process = TryStartVisibleProcess("cmd.exe", cmdArgs);
		}
		else
		{
			process = TryStartVisibleProcess(claudePath, "auth login", configDir);
		}

		if (process == null)
		{
			_log.Error("Failed to start claude auth login for configDir {ConfigDir}", configDir);
			return;
		}

		using (process)
		{
			await process.WaitForExitAsync();
			_log.Information("Auth login for configDir {ConfigDir} exited with code {ExitCode}", configDir, process.ExitCode);
		}
	}

	private async Task<string?> RunClaudeCommandAsync(string claudePath, string args, string? configDir)
	{
		Process? process = TryStartHiddenProcess(claudePath, args, configDir);

		if (process == null && OperatingSystem.IsWindows())
		{
			var cmdArgs = $"/c \"{claudePath}\" {args}";
			process = TryStartHiddenProcess("cmd.exe", cmdArgs, configDir);
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

	private static Process? TryStartHiddenProcess(string fileName, string arguments, string? configDir)
	{
		var psi = new ProcessStartInfo(fileName, arguments)
		{
			RedirectStandardOutput = true,
			RedirectStandardError  = true,
			UseShellExecute        = false,
			CreateNoWindow         = true,
			StandardOutputEncoding = Encoding.UTF8,
		};

		if (!string.IsNullOrEmpty(configDir))
			psi.Environment["CLAUDE_CONFIG_DIR"] = configDir;

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

	private static Process? TryStartVisibleProcess(string fileName, string arguments, string? configDir = null)
	{
		var psi = new ProcessStartInfo(fileName, arguments)
		{
			UseShellExecute = true,
			CreateNoWindow  = false,
		};

		// Note: UseShellExecute=true does not support psi.Environment modifications.
		// For visible processes, CLAUDE_CONFIG_DIR is set via "set" command in the
		// cmd.exe /c wrapper on Windows, or passed directly on other platforms.

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
