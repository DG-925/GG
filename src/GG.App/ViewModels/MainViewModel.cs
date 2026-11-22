using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GG.Core.Logging;
using GG.Core.Models;
using GG.Core.Services;
using GG.Core.Storage;
using GG.App.Services;
using GG.Scanners;

namespace GG.App.ViewModels;

/// <summary>
/// The main window: sidebar filters, search, sort, grid size and the game grid itself.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IGameActions
{
    private readonly GameLibrary _library;
    private readonly SettingsService _settings;
    private readonly AppPaths _paths;
    private readonly IReadOnlySet<GameSource> _installedLaunchers;
    private readonly IDialogService _dialogs;
    private readonly ScanCoordinator _scanner;
    private readonly Dispatcher _dispatcher;
    // The grid is rebuilt as a whole (new list + new view) whenever the library changes.
    // Mutating a view while its refresh is deferred throws in WPF, so we never edit it in place.
    private List<GameViewModel> _games = new();
    private ListCollectionView _gamesView;

    private readonly SidebarItemViewModel _allItem;
    private readonly SidebarItemViewModel[] _fixedItems;

    /// <param name="installedLaunchers">Launchers found on this PC; only these get a sidebar entry.</param>
    public MainViewModel(
        GameLibrary library,
        SettingsService settings,
        AppPaths paths,
        IReadOnlySet<GameSource> installedLaunchers,
        IDialogService dialogs,
        ScanCoordinator scanner)
    {
        _installedLaunchers = installedLaunchers;
        _dialogs = dialogs;
        _scanner = scanner;
        _library = library;
        _settings = settings;
        _paths = paths;
        _dispatcher = Dispatcher.CurrentDispatcher;


        // Fixed sidebar entries; launcher entries are added below them as games appear.
        _allItem = new SidebarItemViewModel(SidebarKind.All, "All Games", "");
        _fixedItems = new[]
        {
            _allItem,
            new SidebarItemViewModel(SidebarKind.Favorites, "Favorites", ""),
            new SidebarItemViewModel(SidebarKind.RecentlyPlayed, "Recently Played", ""),
            new SidebarItemViewModel(SidebarKind.NeedsReview, "Needs Review", ""),
            new SidebarItemViewModel(SidebarKind.Hidden, "Hidden", ""),
            new SidebarItemViewModel(SidebarKind.Header, "LAUNCHERS"),
        };
        foreach (var item in _fixedItems)
            SidebarItems.Add(item);

        // Restore last UI preferences.
        _cardWidth = Math.Clamp(settings.Current.CardWidth, 120, 300);
        _selectedSort = SortOption.All.FirstOrDefault(o => o.Order == settings.Current.SortOrder) ?? SortOption.All[0];
        _selectedSidebarItem = _allItem;
        _gamesView = CreateView(_games);

        _library.Changed += (_, _) => _dispatcher.BeginInvoke(ReloadFromLibrary);
        ReloadFromLibrary();
    }

    /// <summary>The filtered and sorted games shown in the grid.</summary>
    public ICollectionView GamesView => _gamesView;

    public ObservableCollection<SidebarItemViewModel> SidebarItems { get; } = new();

    public IReadOnlyList<SortOption> SortOptions => SortOption.All;

    [ObservableProperty]
    private SidebarItemViewModel? _selectedSidebarItem;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private SortOption _selectedSort;

    /// <summary>Desired card width, bound to the grid size slider.</summary>
    [ObservableProperty]
    private double _cardWidth;

    /// <summary>Number of games currently visible in the grid.</summary>
    [ObservableProperty]
    private int _visibleCount;

    /// <summary>Short message in the status bar (null hides it).</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>True while a scan runs (shows the spinner; the Scan button disables itself).</summary>
    [ObservableProperty]
    private bool _isScanning;

    public string HeaderTitle => SelectedSidebarItem?.Title ?? "All Games";

    // ----- Property change hooks -----

    partial void OnSelectedSidebarItemChanged(SidebarItemViewModel? value)
    {
        if (value is null || value.IsHeader)
        {
            // Never leave the sidebar without a selection.
            SelectedSidebarItem = _allItem;
            return;
        }

        // "Recently Played" only makes sense sorted by date.
        if (value.Kind == SidebarKind.RecentlyPlayed && SelectedSort.Order != SortOrder.LastPlayed)
            SelectedSort = SortOption.All.First(o => o.Order == SortOrder.LastPlayed);

        OnPropertyChanged(nameof(HeaderTitle));
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedSortChanged(SortOption value)
    {
        if (value is null)
            return;
        _gamesView.CustomSort = new GameComparer(value.Order); // also refreshes the view
        _settings.Current.SortOrder = value.Order;
        _settings.RequestSave();
    }

    partial void OnCardWidthChanged(double value)
    {
        _settings.Current.CardWidth = value;
        _settings.RequestSave();
    }

    // ----- Commands -----

    /// <summary>
    /// Scans every source in the background and merges the results into the library.
    /// The first time, asks for the shortcut folder.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Current.ShortcutFolder) && !ChooseShortcutFolderCore())
            return;

        IsScanning = true;
        StatusMessage = "Scanning for games...";
        try
        {
            var progress = new Progress<string>(text => StatusMessage = text); // reports land on the UI thread
            var context = new ScanContext(_settings.Current, progress);

            var results = await Task.Run(() => _scanner.ScanAsync(context));

            var scanned = results.SelectMany(r => r.Games).ToList();
            var scannedSources = results.Where(r => r.SourceAvailable).Select(r => r.Source).ToHashSet();
            var existing = _library.Games;
            var merge = await Task.Run(() => LibraryMerger.Merge(existing, scanned, scannedSources));

            _library.ReplaceAll(merge.Games);
            StatusMessage = BuildSummary(merge, results);
            Log.Info(StatusMessage);
        }
        catch (Exception ex)
        {
            Log.Error("Scan failed", ex);
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>Lets the user pick (or change) the shortcut folder, then rescans.</summary>
    [RelayCommand]
    private async Task ChooseShortcutFolderAsync()
    {
        if (ChooseShortcutFolderCore())
            await ScanAsync();
    }

    private bool ChooseShortcutFolderCore()
    {
        var folder = _dialogs.PickFolder("Choose the folder that holds your game shortcuts", _settings.Current.ShortcutFolder);
        if (folder is null)
            return false;

        _settings.Current.ShortcutFolder = folder;
        _settings.RequestSave();
        return true;
    }

    /// <summary>"Found 165 games: 120 Shortcuts, 40 Steam, 5 Epic Games (+3 new, 1 removed)".</summary>
    private string BuildSummary(MergeResult merge, IReadOnlyList<ScanResult> results)
    {
        var visible = merge.Games.Where(g => !g.IsHidden).ToList();
        var parts = visible.GroupBy(g => g.Source)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} {g.Key.DisplayName()}");

        var summary = $"Found {visible.Count} games";
        if (visible.Count > 0)
            summary += ": " + string.Join(", ", parts);

        var changes = new List<string>();
        if (merge.Added > 0) changes.Add($"+{merge.Added} new");
        if (merge.Removed > 0) changes.Add($"{merge.Removed} removed");
        if (changes.Count > 0)
            summary += $" ({string.Join(", ", changes)})";

        // Mention sources that couldn't be read (e.g. the shortcut folder was moved).
        var problems = results.Where(r => !r.SourceAvailable && r.Message is not null && r.Source == GameSource.Shortcut)
            .Select(r => r.Message);
        foreach (var problem in problems)
            summary += $". {problem}";

        return summary;
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    /// <summary>Opens %AppData%\GG in Explorer (handy to inspect library.json).</summary>
    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_paths.Root}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Could not open data folder", ex);
            StatusMessage = $"Could not open {_paths.Root}";
        }
    }

    // ----- IGameActions -----

    public void Play(GameViewModel game)
    {
        var result = GameLauncher.Launch(game.Model);
        if (result.Success)
        {
            game.Model.LastPlayed = DateTime.Now; // playtime tracking arrives in step 6
            game.Refresh();
            _library.RequestSave();
            UpdateCounts();
            StatusMessage = result.Message;
            return;
        }

        if (result.Error == LaunchError.Cancelled)
        {
            StatusMessage = result.Message;
            return;
        }

        // Offer "Locate exe..." unless the game is started through a launcher URI that we can't fix by picking a file.
        var canLocate = result.Error is LaunchError.MissingFile or LaunchError.NothingToLaunch or LaunchError.Failed;
        if (_dialogs.ShowLaunchError(game.Name, result.Message, canLocate) != LaunchErrorChoice.LocateExe)
            return;

        var startFolder = Directory.Exists(game.Model.InstallPath ?? "") ? game.Model.InstallPath
            : Path.GetDirectoryName(game.Model.ExePath ?? "");
        var exe = _dialogs.PickExe($"Locate the program that starts {game.Name}", startFolder);
        if (exe is null)
            return;

        // The user's choice wins from now on: later scans never overwrite it.
        var model = game.Model;
        model.ExePath = exe;
        model.WorkingDirectory = Path.GetDirectoryName(exe);
        model.LaunchUri = null;
        model.InstallPath ??= Path.GetDirectoryName(exe);
        model.UserOverrodeExe = true;
        model.NeedsReview = false;
        game.Refresh();
        _library.RequestSave();

        Play(game);
    }

    /// <summary>Runs a scan right after start-up when "rescan on startup" is on and there is something to scan.</summary>
    public void StartupScan()
    {
        if (_settings.Current.RescanOnStartup && !string.IsNullOrWhiteSpace(_settings.Current.ShortcutFolder))
            ScanCommand.Execute(null);
    }

    public void ToggleFavorite(GameViewModel game)
    {
        game.IsFavorite = !game.IsFavorite;
        _library.RequestSave();
        UpdateCounts();
        if (SelectedSidebarItem?.Kind == SidebarKind.Favorites)
            ApplyFilter();
    }

    // ----- Internals -----

    /// <summary>Rebuilds the grid from the library (on load and after scans).</summary>
    private void ReloadFromLibrary()
    {
        _games = _library.Games.Select(g => new GameViewModel(g, this)).ToList();
        _gamesView = CreateView(_games);
        OnPropertyChanged(nameof(GamesView));

        UpdateCounts();
        VisibleCount = _gamesView.Count;
    }

    /// <summary>A filtered, sorted view over <paramref name="games"/> using the current search/sidebar/sort.</summary>
    private ListCollectionView CreateView(List<GameViewModel> games)
    {
        var view = new ListCollectionView(games)
        {
            Filter = FilterGame,
            CustomSort = new GameComparer(SelectedSort.Order),
        };
        return view;
    }

    private bool FilterGame(object item)
    {
        if (item is not GameViewModel game)
            return false;

        if (!(SelectedSidebarItem ?? _allItem).Matches(game))
            return false;

        var search = SearchText?.Trim();
        return string.IsNullOrEmpty(search) || game.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void ApplyFilter()
    {
        _gamesView.Refresh();
        VisibleCount = _gamesView.Count;
    }

    /// <summary>Recomputes sidebar counts and adds/removes launcher entries.</summary>
    private void UpdateCounts()
    {
        foreach (var item in _fixedItems)
            item.Count = item.IsHeader ? 0 : _games.Count(item.Matches);

        // One entry per source that has games, in enum order. Launchers (Steam, Epic...) must also be
        // installed on this PC; Shortcuts / Manual / Folder scan only need games.
        var bySource = _games.Where(g => !g.IsHidden)
            .GroupBy(g => g.Source)
            .ToDictionary(g => g.Key, g => g.Count());

        var existing = SidebarItems.Where(i => i.Kind == SidebarKind.Source).ToDictionary(i => i.Source!.Value);
        var insertAt = _fixedItems.Length;

        foreach (var source in Enum.GetValues<GameSource>())
        {
            bySource.TryGetValue(source, out var count);
            existing.TryGetValue(source, out var item);

            var isLauncher = GameSourceInfo.Launchers.Contains(source);
            if (count == 0 || (isLauncher && !_installedLaunchers.Contains(source)))
            {
                if (item is not null)
                {
                    if (ReferenceEquals(SelectedSidebarItem, item))
                        SelectedSidebarItem = _allItem;
                    SidebarItems.Remove(item);
                }
                continue;
            }

            if (item is null)
            {
                item = new SidebarItemViewModel(SidebarKind.Source, source.DisplayName(), "", source);
                SidebarItems.Insert(insertAt, item);
            }

            item.Count = count;
            insertAt++;
        }
    }
}
