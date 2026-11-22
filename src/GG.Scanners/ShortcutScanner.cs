using GG.Core.Logging;
using GG.Core.Models;
using GG.Scanners.Interop;

namespace GG.Scanners;

/// <summary>
/// Imports a folder of .lnk and .url shortcuts (subfolders included). The file name becomes the game name.
/// This is the most trusted source: when the same game is also found by a launcher, the shortcut's target wins.
/// </summary>
public sealed class ShortcutScanner : IGameScanner
{
    /// <summary>Launcher programs: a shortcut to one of these isn't "installed" in the launcher's folder.</summary>
    private static readonly HashSet<string> LauncherClients = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam.exe", "EpicGamesLauncher.exe", "Battle.net.exe", "Battle.net Launcher.exe", "GalaxyClient.exe",
        "upc.exe", "UbisoftConnect.exe", "EADesktop.exe", "EALauncher.exe", "Origin.exe", "XboxPcApp.exe",
    };

    /// <summary>Targets that start something: anything else (readme.txt, manual.pdf...) is skipped.</summary>
    private static readonly HashSet<string> RunnableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".lnk",
    };

    public GameSource Source => GameSource.Shortcut;

    public Task<ScanResult> ScanAsync(ScanContext context, CancellationToken cancellationToken) =>
        Task.Run(() => Scan(context.Settings.ShortcutFolder, context.Progress, cancellationToken), cancellationToken);

    /// <summary>Synchronous scan of <paramref name="folder"/> (public for tests).</summary>
    public ScanResult Scan(string? folder, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return ScanResult.NotAvailable(Source, "No shortcut folder chosen.");
        if (!Directory.Exists(folder))
        {
            Log.Warn($"Shortcut folder not found: {folder}");
            return ScanResult.NotAvailable(Source, $"Shortcut folder not found: {folder}");
        }

        progress?.Report("Reading shortcuts...");
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var games = new List<Game>();

        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file);
            try
            {
                Game? game = null;
                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    game = FromLnk(file);
                else if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
                    game = FromUrl(file);

                if (game is not null)
                    games.Add(game);
            }
            catch (Exception ex)
            {
                Log.Warn($"Skipped shortcut {file}: {ex.Message}");
            }
        }

        Log.Info($"Shortcut scan of {folder}: {games.Count} games.");
        return ScanResult.Found(Source, games);
    }

    private static Game? FromLnk(string lnkPath)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var name = GameNameFromFile(lnkPath);
        if (IsJunkName(name))
            return null;

        var info = ShellLink.Read(lnkPath);
        var game = NewGame(name, lnkPath);

        if (string.IsNullOrWhiteSpace(info.TargetPath))
        {
            // No file target (Store / Xbox app shortcut, special folder): let the shell open the .lnk itself.
            game.LaunchUri = lnkPath;
            return game;
        }

        if (Directory.Exists(info.TargetPath))
            return null; // a shortcut to a folder isn't a game

        var targetExt = Path.GetExtension(info.TargetPath);
        if (!RunnableExtensions.Contains(targetExt))
            return null;

        var targetFile = Path.GetFileName(info.TargetPath);
        if (IsJunkName(Path.GetFileNameWithoutExtension(targetFile)))
            return null;

        game.ExePath = info.TargetPath;
        game.Arguments = string.IsNullOrWhiteSpace(info.Arguments) ? null : info.Arguments;
        game.WorkingDirectory = string.IsNullOrWhiteSpace(info.WorkingDirectory) ? null : info.WorkingDirectory;

        // The game's folder (used later to recognise its process). Not for launcher clients:
        // "steam.exe -applaunch 123" lives in Steam's folder, not the game's.
        if (!LauncherClients.Contains(targetFile))
            game.InstallPath = Path.GetDirectoryName(info.TargetPath);

        ParseLauncherArguments(game, targetFile, info.Arguments);
        return game;
    }

    /// <summary>Reads the URL= line of an Internet shortcut. Web links (http/https) are skipped.</summary>
    internal static Game? FromUrl(string urlPath)
    {
        var name = GameNameFromFile(urlPath);
        if (IsJunkName(name))
            return null;

        string? url = null;
        foreach (var raw in File.ReadLines(urlPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
            {
                url = line[4..].Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            return null;

        var game = NewGame(name, urlPath);
        if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var fileUri))
        {
            game.ExePath = fileUri.LocalPath;
            game.InstallPath = Path.GetDirectoryName(fileUri.LocalPath);
        }
        else
        {
            game.LaunchUri = url;
            ParseLauncherUri(game, url);
        }
        return game;
    }

    /// <summary>Remembers launcher ids in the URI so the shortcut merges with that launcher's entry.</summary>
    internal static void ParseLauncherUri(Game game, string uri)
    {
        // steam://rungameid/367520  or  steam://run/367520
        foreach (var prefix in new[] { "steam://rungameid/", "steam://run/", "steam://launch/" })
        {
            if (uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var id = new string(uri[prefix.Length..].TakeWhile(char.IsDigit).ToArray());
                game.SetExternalId(GameSource.Steam, id);
                return;
            }
        }

        // com.epicgames.launcher://apps/<namespace>%3A<catalogItemId>%3A<AppName>?action=launch&silent=true
        const string epic = "com.epicgames.launcher://apps/";
        if (uri.StartsWith(epic, StringComparison.OrdinalIgnoreCase))
        {
            var path = uri[epic.Length..];
            var q = path.IndexOf('?');
            if (q >= 0)
                path = path[..q];
            var parts = Uri.UnescapeDataString(path).Split(':');
            game.SetExternalId(GameSource.Epic, parts[^1]);
        }
    }

    /// <summary>"steam.exe -applaunch 367520" style shortcuts.</summary>
    private static void ParseLauncherArguments(Game game, string targetFile, string arguments)
    {
        if (!targetFile.Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
            return;
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var i = Array.FindIndex(parts, p => p.Equals("-applaunch", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 < parts.Length)
            game.SetExternalId(GameSource.Steam, parts[i + 1]);
    }

    private static Game NewGame(string name, string shortcutPath)
    {
        var game = new Game { Name = name, Source = GameSource.Shortcut, SourceId = shortcutPath };
        game.SetExternalId(GameSource.Shortcut, shortcutPath);
        return game;
    }

    /// <summary>"Hollow Knight - Shortcut.lnk" → "Hollow Knight".</summary>
    internal static string GameNameFromFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).Trim();
        foreach (var suffix in new[] { " - Shortcut", " - Raccourci", " - Verknüpfung", " - Acceso directo" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name = name[..^suffix.Length].Trim();
        }
        return name;
    }

    /// <summary>Uninstallers, readmes and websites that often sit next to game shortcuts.</summary>
    internal static bool IsJunkName(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Length == 0
            || n.StartsWith("unins") || n.Contains("uninstall")
            || n.Contains("readme") || n.Contains("read me") || n == "manual" || n.EndsWith(" manual")
            || n.Contains("website") || n.Contains("homepage") || n.Contains("support");
    }
}
