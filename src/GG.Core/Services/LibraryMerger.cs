using GG.Core.Logging;
using GG.Core.Models;

namespace GG.Core.Services;

/// <summary>Outcome of merging scan results into the library.</summary>
public sealed record MergeResult(List<Game> Games, int Added, int Updated, int Removed);

/// <summary>
/// Folds freshly scanned games into the existing library:
/// <list type="bullet">
/// <item>the same game found again keeps its id, favorite, hidden flag, playtime, cover and date added;</item>
/// <item>duplicates across sources merge into one entry (match by shared id, then install path, then normalized name);</item>
/// <item>the launch details come from the highest-priority source: user's manual choice &gt; shortcut &gt; launcher &gt; folder scan;</item>
/// <item>games that a successful scan no longer finds are removed, unless they are favorites or have playtime.</item>
/// </list>
/// Pure function: works on copies, never touches the inputs.
/// </summary>
public static class LibraryMerger
{
    /// <param name="existing">The current library.</param>
    /// <param name="scanned">Everything the scanners found this time.</param>
    /// <param name="scannedSources">Sources that were scanned successfully (only these can have stale entries removed).</param>
    public static MergeResult Merge(IReadOnlyList<Game> existing, IReadOnlyList<Game> scanned, IReadOnlySet<GameSource> scannedSources)
    {
        // Sample games from development step 1 go away as soon as real games arrive.
        var result = existing.Where(g => !SampleData.IsSample(g)).Select(g => g.Clone()).ToList();

        // Which sources matched each entry during this run (an entry can absorb one game per source).
        var matchedBy = new Dictionary<Game, HashSet<GameSource>>(ReferenceEqualityComparer.Instance);
        int added = 0, updated = 0;

        // Higher priority first, so e.g. a shortcut claims its entry before a launcher looks for a match.
        foreach (var found in scanned.OrderByDescending(g => GameMatching.Priority(g.Source)))
        {
            var incoming = found.Clone();
            incoming.SetExternalId(incoming.Source, incoming.SourceId);

            var match = FindMatch(result, incoming, matchedBy);
            if (match is null)
            {
                incoming.DateAdded = DateTime.Now;
                result.Add(incoming);
                matchedBy[incoming] = new HashSet<GameSource> { incoming.Source };
                added++;
            }
            else
            {
                MergeInto(match, incoming);
                if (!matchedBy.TryGetValue(match, out var sources))
                    matchedBy[match] = sources = new HashSet<GameSource>();
                sources.Add(incoming.Source);
                updated++;
            }
        }

        // Drop entries a successful scan no longer finds.
        var removed = result.RemoveAll(g =>
        {
            if (!scannedSources.Contains(g.Source) || matchedBy.ContainsKey(g))
                return false;
            if (g.Source == GameSource.Manual || g.UserOverrodeExe)
                return false;
            if (g.IsFavorite || g.TotalPlaytime > TimeSpan.Zero)
            {
                Log.Info($"Kept '{g.Name}' although {g.Source} no longer lists it (favorite or has playtime).");
                return false;
            }
            return true;
        });

        return new MergeResult(result, added, updated, removed);
    }

    private static Game? FindMatch(List<Game> library, Game incoming, Dictionary<Game, HashSet<GameSource>> matchedBy)
    {
        bool Available(Game g) => !matchedBy.TryGetValue(g, out var s) || !s.Contains(incoming.Source);

        // 1. Same id (a rescan of the same shortcut / Steam app, or an entry that already merged both).
        var byId = library.FirstOrDefault(g => Available(g) && GameMatching.ShareExternalId(g, incoming));
        if (byId is not null)
            return byId;

        // Path and name matching only pair games from *different* sources: two shortcuts are two games
        // (e.g. several emulator shortcuts pointing into the same folder).
        var candidates = library.Where(g => g.Source != incoming.Source && Available(g)).ToList();

        // 2. Same install location.
        var byPath = candidates.FirstOrDefault(g => GameMatching.SameInstall(g, incoming));
        if (byPath is not null)
            return byPath;

        // 3. Same normalized name.
        var name = GameMatching.NormalizeName(incoming.Name);
        return name.Length == 0 ? null : candidates.FirstOrDefault(g => GameMatching.NormalizeName(g.Name) == name);
    }

    /// <summary>Updates <paramref name="target"/> (already in the library) with what was just found.</summary>
    private static void MergeInto(Game target, Game incoming)
    {
        foreach (var (source, id) in incoming.ExternalIds)
            target.ExternalIds.TryAdd(source, id);

        var incomingWins = GameMatching.Priority(incoming.Source) >= GameMatching.Priority(target.Source);

        if (incomingWins && !target.UserOverrodeExe)
        {
            target.Source = incoming.Source;
            target.SourceId = incoming.SourceId;
            target.ExePath = incoming.ExePath;
            target.LaunchUri = incoming.LaunchUri;
            target.Arguments = incoming.Arguments;
            target.WorkingDirectory = incoming.WorkingDirectory;
            target.NeedsReview = incoming.NeedsReview;
            target.ExeCandidates = incoming.ExeCandidates;
            if (!string.IsNullOrWhiteSpace(incoming.InstallPath))
                target.InstallPath = incoming.InstallPath;
            if (!target.UserRenamed)
                target.Name = incoming.Name;
        }
        else
        {
            // Lower-priority finds only fill gaps (e.g. a Steam install folder for a steam:// shortcut,
            // which lets playtime tracking find the game's process).
            if (string.IsNullOrWhiteSpace(target.InstallPath))
                target.InstallPath = incoming.InstallPath;
        }

        if (string.IsNullOrWhiteSpace(target.IconPath))
            target.IconPath = incoming.IconPath;
    }
}
