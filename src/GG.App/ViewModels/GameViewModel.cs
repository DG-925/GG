using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GG.App.Services;
using GG.Core.Models;

namespace GG.App.ViewModels;

/// <summary>
/// Wraps a <see cref="Game"/> for the UI. Setters write straight through to the model,
/// so saving the library saves the change.
/// </summary>
public sealed partial class GameViewModel : ObservableObject
{
    private const string StarOutline = "";
    private const string StarFilled = "";

    private readonly IGameActions _actions;
    private ImageSource? _cover;
    private ImageSource? _icon;
    private bool _coverLoaded;
    private bool _iconLoaded;

    public GameViewModel(Game model, IGameActions actions)
    {
        Model = model;
        _actions = actions;
    }

    public Game Model { get; }

    public string Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set => SetProperty(Model.Name, value, Model, (m, v) => m.Name = v);
    }

    public GameSource Source => Model.Source;

    public string SourceLabel => Model.Source.DisplayName();

    public bool IsFavorite
    {
        get => Model.IsFavorite;
        set
        {
            if (SetProperty(Model.IsFavorite, value, Model, (m, v) => m.IsFavorite = v))
                OnPropertyChanged(nameof(FavoriteGlyph));
        }
    }

    public string FavoriteGlyph => IsFavorite ? StarFilled : StarOutline;

    public bool IsHidden
    {
        get => Model.IsHidden;
        set => SetProperty(Model.IsHidden, value, Model, (m, v) => m.IsHidden = v);
    }

    public bool NeedsReview => Model.NeedsReview;

    public DateTime? LastPlayed => Model.LastPlayed;

    public TimeSpan TotalPlaytime => Model.TotalPlaytime;

    public DateTime DateAdded => Model.DateAdded;

    /// <summary>"12 h 30 min played", "45 min played" or "Not played yet".</summary>
    public string PlaytimeText
    {
        get
        {
            var t = Model.TotalPlaytime;
            if (t < TimeSpan.FromMinutes(1))
                return "Not played yet";
            if (t.TotalHours < 1)
                return $"{(int)t.TotalMinutes} min played";
            return t.Minutes == 0
                ? $"{(int)t.TotalHours} h played"
                : $"{(int)t.TotalHours} h {t.Minutes} min played";
        }
    }

    /// <summary>Cover art, loaded on first use. Null → the fallback card is shown.</summary>
    public ImageSource? CoverImage
    {
        get
        {
            if (!_coverLoaded)
            {
                _coverLoaded = true;
                _cover = ImageLoader.Load(Model.CoverPath, 400);
            }
            return _cover;
        }
    }

    /// <summary>Icon extracted from the exe, shown on the fallback card.</summary>
    public ImageSource? IconImage
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                _icon = ImageLoader.Load(Model.IconPath, 128);
            }
            return _icon;
        }
    }

    [RelayCommand]
    private void Play() => _actions.Play(this);

    [RelayCommand]
    private void ToggleFavorite() => _actions.ToggleFavorite(this);

    /// <summary>Re-reads everything from the model (after a rescan or edit).</summary>
    public void Refresh()
    {
        _coverLoaded = _iconLoaded = false;
        OnPropertyChanged(string.Empty);
    }
}
