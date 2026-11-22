using System.Collections;
using GG.Core.Models;

namespace GG.App.ViewModels;

/// <summary>An entry in the sort drop-down.</summary>
public sealed record SortOption(SortOrder Order, string Label)
{
    public static IReadOnlyList<SortOption> All { get; } = new[]
    {
        new SortOption(SortOrder.Name, "Name"),
        new SortOption(SortOrder.LastPlayed, "Last played"),
        new SortOption(SortOrder.Playtime, "Playtime"),
        new SortOption(SortOrder.DateAdded, "Date added"),
    };

    public override string ToString() => Label;
}

/// <summary>Sorts <see cref="GameViewModel"/>s for the grid. Ties fall back to name.</summary>
public sealed class GameComparer : IComparer
{
    private static readonly StringComparer NameComparer = StringComparer.CurrentCultureIgnoreCase;
    private readonly SortOrder _order;

    public GameComparer(SortOrder order) => _order = order;

    public int Compare(object? x, object? y)
    {
        if (x is not GameViewModel a || y is not GameViewModel b)
            return 0;

        var result = _order switch
        {
            // Most recent first; never-played games go to the end.
            SortOrder.LastPlayed => Nullable.Compare(b.LastPlayed, a.LastPlayed),
            SortOrder.Playtime => b.TotalPlaytime.CompareTo(a.TotalPlaytime),
            SortOrder.DateAdded => b.DateAdded.CompareTo(a.DateAdded),
            _ => 0,
        };

        return result != 0 ? result : NameComparer.Compare(a.Name, b.Name);
    }
}
