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

    /// <summary>
    /// Every id this game is known by, keyed by source name (e.g. "Steam" → "367520", "Shortcut" → the .lnk path).
    /// When a shortcut and a launcher find the same game they merge into one entry that keeps both ids,
    /// so rescans recognise it from either side and Steam covers can still use the appid.
    /// </summary>
    public Dictionary<string, string> ExternalIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Root folder of the installed game, if known.</summary>
    public string? InstallPath { get; set; }

    /// <summary>The executable to start. Either this or <see cref="LaunchUri"/> is used to launch.</summary>
    public string? ExePath { get; set; }

    /// <summary>
    /// What to hand to the Windows shell to start the game: a launcher URI (steam://..., com.epicgames.launcher://...,
    /// shell:AppsFolder\...) or a shortcut file that has no plain exe target (Store app shortcuts).
    /// Used instead of <see cref="ExePath"/> when set.
    /// </summary>
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

    /// <summary>True once the user renamed the game; scans keep the user's name.</summary>
    public bool UserRenamed { get; set; }

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
        copy.ExternalIds = new Dictionary<string, string>(ExternalIds ?? new(), StringComparer.OrdinalIgnoreCase);
        return copy;
    }

    /// <summary>Records an id for <paramref name="source"/> (ignored when empty).</summary>
    public void SetExternalId(GameSource source, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        ExternalIds ??= new(StringComparer.OrdinalIgnoreCase);
        ExternalIds[source.ToString()] = id;
    }

    /// <summary>The id this game has in <paramref name="source"/>, or null.</summary>
    public string? GetExternalId(GameSource source) =>
        ExternalIds is not null && ExternalIds.TryGetValue(source.ToString(), out var id) ? id : null;

    public override string ToString() => $"{Name} ({Source})";
}
