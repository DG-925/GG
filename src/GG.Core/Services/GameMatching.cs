using System.Text;
using GG.Core.Models;

namespace GG.Core.Services;

/// <summary>Helpers for recognising the same game found in different places.</summary>
public static class GameMatching
{
    /// <summary>
    /// "The Witcher® 3: Wild Hunt" → "thewitcher3wildhunt". Lower-case, symbols and punctuation removed,
    /// "&amp;" read as "and", so small spelling differences between launchers still match.
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Replace("&", "and"))
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>Lower-case, backslashes, no trailing separator. Null/blank → "".</summary>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        return path.Trim().Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
    }

    /// <summary>True if <paramref name="path"/> is inside (or equal to) <paramref name="folder"/>.</summary>
    public static bool IsInside(string? path, string? folder)
    {
        var p = NormalizePath(path);
        var f = NormalizePath(folder);
        if (p.Length == 0 || !IsSpecificFolder(f))
            return false;
        return p == f || p.StartsWith(f + "\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// Guards against matching on folders that hold many games (a drive root like "d:" or "d:\games").
    /// A game folder needs at least two levels below the drive.
    /// </summary>
    public static bool IsSpecificFolder(string normalizedFolder) =>
        normalizedFolder.Length > 0 && normalizedFolder.Count(c => c == '\\') >= 2;

    /// <summary>Do the two games share any (source, id) pair?</summary>
    public static bool ShareExternalId(Game a, Game b)
    {
        if (a.ExternalIds is null || b.ExternalIds is null)
            return false;
        foreach (var (source, id) in a.ExternalIds)
        {
            if (b.ExternalIds.TryGetValue(source, out var other) && string.Equals(id, other, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Same installed game by location: equal install folders, or one's exe inside the other's install folder.
    /// Shortcut "install folders" are just the exe's folder, so they never count as the containing folder.
    /// </summary>
    public static bool SameInstall(Game a, Game b)
    {
        var pa = NormalizePath(a.InstallPath);
        var pb = NormalizePath(b.InstallPath);
        if (IsSpecificFolder(pa) && pa == pb)
            return true;

        return (b.Source != GameSource.Shortcut && IsInside(a.ExePath, b.InstallPath))
            || (a.Source != GameSource.Shortcut && IsInside(b.ExePath, a.InstallPath));
    }

    /// <summary>
    /// Which source wins when the same game is found twice: the user's manual entry, then a shortcut
    /// (the user made it, so its exe is trusted), then a launcher, then a folder scan guess.
    /// </summary>
    public static int Priority(GameSource source) => source switch
    {
        GameSource.Manual => 4,
        GameSource.Shortcut => 3,
        GameSource.FolderScan => 1,
        _ => 2, // launchers
    };
}
