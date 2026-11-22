namespace GG.Core.Models;

/// <summary>Where a game entry came from.</summary>
public enum GameSource
{
    Steam,
    Epic,
    Xbox,
    GOG,
    EA,
    Ubisoft,
    BattleNet,
    Shortcut,
    Manual,
    FolderScan,
}

/// <summary>Display helpers for <see cref="GameSource"/>.</summary>
public static class GameSourceInfo
{
    /// <summary>Human-friendly name used in the sidebar and summaries.</summary>
    public static string DisplayName(this GameSource source) => source switch
    {
        GameSource.Steam => "Steam",
        GameSource.Epic => "Epic Games",
        GameSource.Xbox => "Xbox",
        GameSource.GOG => "GOG",
        GameSource.EA => "EA app",
        GameSource.Ubisoft => "Ubisoft Connect",
        GameSource.BattleNet => "Battle.net",
        GameSource.Shortcut => "Shortcuts",
        GameSource.Manual => "Manual",
        GameSource.FolderScan => "Folder scan",
        _ => source.ToString(),
    };

    /// <summary>The launcher-based sources (as opposed to user-driven ones).</summary>
    public static IReadOnlyList<GameSource> Launchers { get; } = new[]
    {
        GameSource.Steam, GameSource.Epic, GameSource.Xbox, GameSource.GOG,
        GameSource.EA, GameSource.Ubisoft, GameSource.BattleNet,
    };
}
