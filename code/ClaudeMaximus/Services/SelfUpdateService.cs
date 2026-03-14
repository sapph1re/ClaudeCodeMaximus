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
	private const string AssemblyFileName = "ClaudeMaximus.dll";

	private static readonly int[] RetryDelays = { 1, 2, 4, 8, 16, 32, 64 };

	public void CheckAndTriggerUpdate()
	{
		try
		{
			var runningDir   = AppContext.BaseDirectory.TrimEnd('\\', '/');
			var solutionRoot = FindSolutionRoot(runningDir);

			if (solutionRoot == null)
			{
				Log.Information("SelfUpdate: solution root not found from {RunningDir}, skipping.", runningDir);
				return;
			}

			Log.Information("SelfUpdate: solution root = {SolutionRoot}, running from = {RunningDir}", solutionRoot, runningDir);

			var sourceDir = FindNewestBuildOutputDir(solutionRoot, runningDir);
			if (sourceDir == null)
			{
				Log.Information("SelfUpdate: no build output directory found under {SolutionRoot}", solutionRoot);
				return;
			}

			var sourceDll  = Path.Combine(sourceDir,  AssemblyFileName);
			var runningDll = Path.Combine(runningDir, AssemblyFileName);

			Log.Information("SelfUpdate: candidate sourceDir = {SourceDir}", sourceDir);

			if (!File.Exists(runningDll))
			{
				Log.Information("SelfUpdate: running dll not found: {RunningDll}, spawning copy anyway.", runningDll);
				SpawnCopyProcess(sourceDir, runningDir);
				return;
			}

			var srcTime = File.GetLastWriteTimeUtc(sourceDll);
			var runTime = File.GetLastWriteTimeUtc(runningDll);

			if (srcTime <= runTime)
			{
				Log.Information("SelfUpdate: running copy is up-to-date. Source={SrcTime}, Running={RunTime}", srcTime, runTime);
				return;
			}

			Log.Information("SelfUpdate: newer build detected, spawning copy. Source={SrcTime}, Running={RunTime}", srcTime, runTime);
			SpawnCopyProcess(sourceDir, runningDir);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "SelfUpdate: unexpected error during update check.");
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

	/// <summary>
	/// Finds the directory containing the newest ClaudeMaximus.dll under the solution root,
	/// excluding the running directory itself and test project directories.
	/// </summary>
	private static string? FindNewestBuildOutputDir(string solutionRoot, string runningDir)
	{
		try
		{
			var candidates = Directory.GetFiles(solutionRoot, AssemblyFileName, SearchOption.AllDirectories);
			string? bestDir = null;
			DateTime bestTime = DateTime.MinValue;

			foreach (var candidate in candidates)
			{
				var dir = Path.GetDirectoryName(candidate)!;

				// Skip the directory we're running from
				if (dir.Equals(runningDir, StringComparison.OrdinalIgnoreCase))
					continue;

				// Skip test project output
				if (dir.Contains("Tests", StringComparison.OrdinalIgnoreCase))
					continue;

				var writeTime = File.GetLastWriteTimeUtc(candidate);
				Log.Debug("SelfUpdate: found candidate {Dir}, modified {Time}", dir, writeTime);

				if (writeTime > bestTime)
				{
					bestTime = writeTime;
					bestDir = dir;
				}
			}

			return bestDir;
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "SelfUpdate: error searching for build output under {Root}", solutionRoot);
		}

		return null;
	}

	private static void SpawnCopyProcess(string sourceDir, string destDir)
	{
		var scriptPath = Path.Combine(Path.GetTempPath(), "ClaudeMaximus_update.ps1");
		File.WriteAllText(scriptPath, BuildScript(sourceDir, destDir, RetryDelays));

		var psi = new ProcessStartInfo
		{
			FileName         = "powershell.exe",
			Arguments        = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
			UseShellExecute  = true,
			CreateNoWindow   = true,
		};

		Process.Start(psi);
	}

	private static string BuildScript(string sourceDir, string destDir, int[] delays)
	{
		var src  = sourceDir.Replace("'", "''");
		var dst  = destDir.Replace("'", "''");

		return
@$"$source = '{src}'
$dest   = '{dst}'
$delays = @({string.Join(", ", delays)})

for ($i = 0; $i -lt $delays.Length; $i++) {{
    try {{
        $files = Get-ChildItem -Path $source -File
        foreach ($file in $files) {{
            $destFile = Join-Path $dest $file.Name
            Copy-Item -Path $file.FullName -Destination $destFile -Force
        }}
        Write-Host 'ClaudeMaximus update: copy complete.'
        exit 0
    }} catch {{
        $delay = $delays[$i]
        $msg   = $_.Exception.Message
        Write-Host ""ClaudeMaximus update: attempt $($i + 1) failed - $msg. Retrying in $($delay)s...""
        Start-Sleep -Seconds $delay
    }}
}}
Write-Host 'ClaudeMaximus update: all attempts exhausted.'
";
	}
}
