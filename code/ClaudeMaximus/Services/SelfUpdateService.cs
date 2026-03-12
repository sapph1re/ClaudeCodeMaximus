using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClaudeMaximus.Services;

/// <summary>
/// Detects whether a fresh build is available and spawns an out-of-process
/// copy script so the publish directory is updated after the app exits.
/// </summary>
public class SelfUpdateService : ISelfUpdateService
{
	private const string SolutionFileName    = "ClaudeMaximus.sln";
	private const string SourceRelativePath  = @"code\ClaudeMaximus\bin\Debug\net9.0";
	private const string AssemblyFileName    = "ClaudeMaximus.dll";
	private const int    MaxRetries          = 10;

	private static readonly int[] RetryDelays = { 1, 2, 4, 8, 16, 32, 64 };

	public void CheckAndTriggerUpdate()
	{
		try
		{
			var publishDir  = AppContext.BaseDirectory.TrimEnd('\\', '/');
			var solutionRoot = FindSolutionRoot(publishDir);

			if (solutionRoot == null)
			{
				Log.Debug("SelfUpdateService: solution root not found, skipping update check.");
				return;
			}

			var sourceDir  = Path.Combine(solutionRoot, SourceRelativePath);
			var sourceDll  = Path.Combine(sourceDir,   AssemblyFileName);
			var publishDll = Path.Combine(publishDir,  AssemblyFileName);

			if (!Directory.Exists(sourceDir))
			{
				Log.Debug("SelfUpdateService: source dir not found: {SourceDir}", sourceDir);
				return;
			}

			if (!File.Exists(sourceDll))
			{
				Log.Debug("SelfUpdateService: source dll not found: {SourceDll}", sourceDll);
				return;
			}

			if (File.Exists(publishDll)
				&& File.GetLastWriteTimeUtc(sourceDll) <= File.GetLastWriteTimeUtc(publishDll))
			{
				Log.Debug("SelfUpdateService: publish is already up-to-date, no copy needed.");
				return;
			}

			Log.Information("SelfUpdateService: newer build detected, spawning copy process. Source: {Src}", sourceDir);
			SpawnCopyProcess(sourceDir, publishDir);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "SelfUpdateService: unexpected error during update check.");
		}
	}

	// Walk up the directory tree from startDir until a *.sln file is found.
	private static string? FindSolutionRoot(string startDir)
	{
		var current = startDir;
		while (current != null)
		{
			if (Directory.GetFiles(current, "*.sln", SearchOption.TopDirectoryOnly).Any())
				return current;

			current = Directory.GetParent(current)?.FullName;
		}

		return null;
	}

	private static void SpawnCopyProcess(string sourceDir, string destDir)
	{
		var scriptPath = Path.Combine(Path.GetTempPath(), "ClaudeMaximus_update.ps1");
		File.WriteAllText(scriptPath, BuildScript(sourceDir, destDir, MaxRetries, RetryDelays));

		var psi = new ProcessStartInfo
		{
			FileName         = "powershell.exe",
			Arguments        = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
			UseShellExecute  = true,
			CreateNoWindow   = true,
		};

		Process.Start(psi);
	}

	private static string BuildScript(string sourceDir, string destDir, int maxRetries, int[] delays)
	{
		// Escape any single-quotes that might appear in path strings inside the PS script.
		var src  = sourceDir.Replace("'", "''");
		var dst  = destDir.Replace("'", "''");

		return
@$"$source     = '{src}'
$dest       = '{dst}'
$maxRetries = {maxRetries}
$delays     = @({string.Join(", ", delays)})

for ($i = 0; $i -lt $maxRetries; $i++) {{
    try {{
        $files = Get-ChildItem -Path $source -File
        foreach ($file in $files) {{
            $destFile = Join-Path $dest $file.Name
            Copy-Item -Path $file.FullName -Destination $destFile -Force
        }}
        Write-Host 'ClaudeMaximus update: copy complete.'
        break
    }} catch {{
        $delay = if ($i -lt $delays.Length) {{ $delays[$i] }} else {{ 64 }}
        $msg   = $_.Exception.Message
        Write-Host ""ClaudeMaximus update: attempt $($i + 1) failed — $msg. Retrying in ${{delay}}s...""
        Start-Sleep -Seconds $delay
    }}
}}
";
	}
}
