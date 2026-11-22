using CommunityToolkit.Mvvm.ComponentModel;
using GG.Core.Models;

namespace GG.App.ViewModels;

/// <summary>What a sidebar entry shows.</summary>
public enum SidebarKind
{
    All,
    Favorites,
    RecentlyPlayed,
    NeedsReview,
    Hidden,
    Header,
    Source,
}

/// <summary>One sidebar entry: a filter over the library plus its live count.</summary>
public sealed partial class SidebarItemViewModel : ObservableObject
{
    public SidebarItemViewModel(SidebarKind kind, string title, string glyph = "", GameSource? source = null)
    {
        Kind = kind;
        Title = title;
        Glyph = glyph;
        Source = source;
    }

    public SidebarKind Kind { get; }

    public string Title { get; }

    /// <summary>Icon font glyph.</summary>
    public string Glyph { get; }

    /// <summary>Set for launcher entries.</summary>
    public GameSource? Source { get; }

    public bool IsHeader => Kind == SidebarKind.Header;

    [ObservableProperty]
    private int _count;

    /// <summary>Does <paramref name="game"/> belong in this view? Hidden games only show under "Hidden".</summary>
    public bool Matches(GameViewModel game) => Kind switch
    {
        SidebarKind.All => !game.IsHidden,
        SidebarKind.Favorites => game.IsFavorite && !game.IsHidden,
        SidebarKind.RecentlyPlayed => game.LastPlayed is not null && !game.IsHidden,
        SidebarKind.NeedsReview => game.NeedsReview && !game.IsHidden,
        SidebarKind.Hidden => game.IsHidden,
        SidebarKind.Source => game.Source == Source && !game.IsHidden,
        _ => false,
    };
}
