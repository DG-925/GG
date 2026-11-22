namespace GG.Core.Models;

/// <summary>
/// One entry in the library. This is a plain data object that is serialized to library.json;
/// the UI wraps it in a view model for change notification.
/// </summary>
public sealed class Game
{
    /// <summary>Stable unique id (GUID without dashes).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public GameSource Source { get; set; } = GameSource.Manual;

    /// <summary>
    /// Id of the game inside its launcher (Steam appid, Epic AppName, Xbox PackageFamilyName...).
    /// Used for cover lookups and to recognise the same game on rescans. Null for shortcuts/folder scans.
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>Root folder of the installed game, if known.</summary>
    public string? InstallPath { get; set; }

    /// <summary>The executable to start. Either this or <see cref="LaunchUri"/> is used to launch.</summary>
    public string? ExePath { get; set; }

    /// <summary>URI handled by a launcher (steam://..., com.epicgames.launcher://..., shell:AppsFolder\...).</summary>
    public string? LaunchUri { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>Cached icon image (png) extracted from the exe.</summary>
    public string? IconPath { get; set; }

    /// <summary>Cached 600x900 cover image.</summary>
    public string? CoverPath { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsHidden { get; set; }

    public DateTime? LastPlayed { get; set; }

    public TimeSpan TotalPlaytime { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.Now;

    /// <summary>True once the user picked the exe by hand; scans must never overwrite it.</summary>
    public bool UserOverrodeExe { get; set; }

    /// <summary>
    /// True when the exe picker could not clearly decide between candidates (top two scores within 20).
    /// The UI lists these under "Needs review".
    /// </summary>
    public bool NeedsReview { get; set; }

    /// <summary>Exe candidates offered in the "Needs review" dialog (best first).</summary>
    public List<string>? ExeCandidates { get; set; }

    /// <summary>Shallow copy, handy for merging and tests.</summary>
    public Game Clone()
    {
        var copy = (Game)MemberwiseClone();
        copy.ExeCandidates = ExeCandidates is null ? null : new List<string>(ExeCandidates);
        return copy;
    }

    public override string ToString() => $"{Name} ({Source})";
}
