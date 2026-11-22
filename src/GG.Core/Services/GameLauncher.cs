using System.ComponentModel;
using System.Diagnostics;
using GG.Core.Logging;
using GG.Core.Models;

namespace GG.Core.Services;

/// <summary>Why a launch failed, so the UI can offer the right fix.</summary>
public enum LaunchError
{
    None,
    /// <summary>The exe (or shortcut) isn't there any more → offer "Locate exe...".</summary>
    MissingFile,
    /// <summary>The entry has neither an exe nor a URI.</summary>
    NothingToLaunch,
    /// <summary>The user said no to the Windows admin prompt.</summary>
    Cancelled,
    /// <summary>Windows refused for another reason (message has details).</summary>
    Failed,
}

/// <summary>Result of <see cref="GameLauncher.Launch"/>.</summary>
/// <param name="Process">The started process when Windows gives one back (exe launches usually do, URIs don't).</param>
public sealed record LaunchResult(LaunchError Error, string Message, Process? Process = null)
{
    public bool Success => Error == LaunchError.None;
}

/// <summary>Starts games: through their launcher URI when they have one, otherwise their exe.</summary>
public static class GameLauncher
{
    private const int ErrorCancelled = 1223; // the user declined the UAC prompt

    public static LaunchResult Launch(Game game)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(game.LaunchUri))
                return LaunchUri(game);
            if (!string.IsNullOrWhiteSpace(game.ExePath))
                return LaunchExe(game);
            return new LaunchResult(LaunchError.NothingToLaunch, $"GG doesn't know how to start \"{game.Name}\" yet.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return new LaunchResult(LaunchError.Cancelled, "The launch was cancelled.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3) // file / path not found
        {
            Log.Warn($"Launch of {game.Name} failed: {ex.Message}");
            return new LaunchResult(LaunchError.MissingFile, $"Windows couldn't find the file to start \"{game.Name}\".\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error($"Launch of {game.Name} failed", ex);
            return new LaunchResult(LaunchError.Failed, $"\"{game.Name}\" couldn't be started.\n\n{ex.Message}");
        }
    }

    private static LaunchResult LaunchUri(Game game)
    {
        var target = game.LaunchUri!.Trim();

        // A shortcut file used as the target (e.g. Store app shortcut): check it still exists.
        if (LooksLikeFilePath(target) && !File.Exists(target))
            return new LaunchResult(LaunchError.MissingFile, $"The shortcut for \"{game.Name}\" is missing:\n{target}");

        Log.Info($"Launching {game.Name} via {target}");
        var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        return new LaunchResult(LaunchError.None, $"Started {game.Name}", process);
    }

    private static LaunchResult LaunchExe(Game game)
    {
        var exe = Environment.ExpandEnvironmentVariables(game.ExePath!.Trim().Trim('"'));
        if (!File.Exists(exe))
            return new LaunchResult(LaunchError.MissingFile, $"The game file for \"{game.Name}\" is missing:\n{exe}");

        var workingDir = game.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
            workingDir = Path.GetDirectoryName(exe);

        Log.Info($"Launching {game.Name}: \"{exe}\" {game.Arguments}");
        // UseShellExecute lets games that need admin show the normal Windows prompt.
        var process = Process.Start(new ProcessStartInfo(exe)
        {
            Arguments = game.Arguments ?? "",
            WorkingDirectory = workingDir ?? "",
            UseShellExecute = true,
        });
        return new LaunchResult(LaunchError.None, $"Started {game.Name}", process);
    }

    /// <summary>"C:\..." or "\\server\..." rather than "steam://..." / "shell:...".</summary>
    private static bool LooksLikeFilePath(string target) =>
        (target.Length > 2 && target[1] == ':' && (target[2] == '\\' || target[2] == '/')) || target.StartsWith(@"\\", StringComparison.Ordinal);
}
