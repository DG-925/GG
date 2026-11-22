namespace GG.Core.Models;

/// <summary>User settings, stored in %AppData%\GG\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Folders like D:\Games whose direct subfolders are treated as games.</summary>
    public List<string> ScanFolders { get; set; } = new();

    /// <summary>Folder of .lnk / .url shortcuts to import.</summary>
    public string? ShortcutFolder { get; set; }

    /// <summary>Optional SteamGridDB API key for cover art. The app works without it.</summary>
    public string? SteamGridDbApiKey { get; set; }

    /// <summary>Launchers that should be scanned.</summary>
    public List<GameSource> EnabledLaunchers { get; set; } = new(GameSourceInfo.Launchers);

    public bool StartWithWindows { get; set; }

    public bool RescanOnStartup { get; set; } = true;

    /// <summary>Set after the welcome screen has been completed.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Preferred cover card width in pixels (grid size slider).</summary>
    public double CardWidth { get; set; } = 180;

    /// <summary>Last used sort order.</summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Name;
}

/// <summary>Sort options in the top bar.</summary>
public enum SortOrder
{
    Name,
    LastPlayed,
    Playtime,
    DateAdded,
}
