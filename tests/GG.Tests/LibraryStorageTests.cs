using GG.Core.Models;
using GG.Core.Services;
using GG.Core.Storage;

namespace GG.Tests;

public class LibraryStorageTests
{
    [Fact]
    public async Task Missing_file_loads_as_empty_library()
    {
        using var dir = new TempDir();
        var library = new GameLibrary(new AppPaths(dir.Path));

        await library.LoadAsync();

        Assert.Empty(library.Games);
        Assert.False(library.ExistedOnDisk);
    }

    [Fact]
    public async Task Games_round_trip_through_json()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var original = new Game
        {
            Name = "Hollow Knight",
            Source = GameSource.Steam,
            SourceId = "367520",
            LaunchUri = "steam://rungameid/367520",
            ExePath = @"C:\Games\Hollow Knight\hollow_knight.exe",
            IsFavorite = true,
            LastPlayed = new DateTime(2026, 11, 1, 20, 30, 0),
            TotalPlaytime = TimeSpan.FromMinutes(754),
            UserOverrodeExe = true,
            ExeCandidates = new() { "a.exe", "b.exe" },
        };

        var library = new GameLibrary(paths);
        library.AddRange(new[] { original });
        await library.SaveAsync();

        var reloaded = new GameLibrary(paths);
        await reloaded.LoadAsync();

        var game = Assert.Single(reloaded.Games);
        Assert.Equal(original.Id, game.Id);
        Assert.Equal("Hollow Knight", game.Name);
        Assert.Equal(GameSource.Steam, game.Source);
        Assert.Equal("367520", game.SourceId);
        Assert.Equal(original.LaunchUri, game.LaunchUri);
        Assert.Equal(original.ExePath, game.ExePath);
        Assert.True(game.IsFavorite);
        Assert.Equal(original.LastPlayed, game.LastPlayed);
        Assert.Equal(TimeSpan.FromMinutes(754), game.TotalPlaytime);
        Assert.True(game.UserOverrodeExe);
        Assert.Equal(new[] { "a.exe", "b.exe" }, game.ExeCandidates);
        Assert.True(reloaded.ExistedOnDisk);
    }

    [Fact]
    public async Task Enums_are_saved_as_text()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var library = new GameLibrary(paths);
        library.AddRange(new[] { new Game { Name = "X", Source = GameSource.BattleNet } });
        await library.SaveAsync();

        var json = await File.ReadAllTextAsync(paths.LibraryFile);
        Assert.Contains("\"BattleNet\"", json);
    }

    [Fact]
    public async Task Second_save_keeps_a_backup()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var library = new GameLibrary(paths);
        library.AddRange(new[] { new Game { Name = "One" } });
        await library.SaveAsync();
        library.AddRange(new[] { new Game { Name = "Two" } });
        await library.SaveAsync();

        Assert.True(File.Exists(paths.LibraryFile + ".bak"));
        Assert.False(File.Exists(paths.LibraryFile + ".tmp"));
    }

    [Fact]
    public async Task Corrupt_file_is_set_aside_and_backup_is_used()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var library = new GameLibrary(paths);
        library.AddRange(new[] { new Game { Name = "Good" } });
        await library.SaveAsync();
        await library.SaveAsync(); // creates library.json.bak with "Good"

        await File.WriteAllTextAsync(paths.LibraryFile, "{ this is not json");

        var reloaded = new GameLibrary(paths);
        await reloaded.LoadAsync();

        Assert.Equal("Good", Assert.Single(reloaded.Games).Name);
        Assert.Contains(Directory.GetFiles(dir.Path), f => f.Contains(".corrupt-"));
    }

    [Fact]
    public async Task Corrupt_file_without_backup_gives_empty_library()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        await File.WriteAllTextAsync(paths.LibraryFile, "garbage");

        var library = new GameLibrary(paths);
        await library.LoadAsync();

        Assert.Empty(library.Games);
    }

    [Fact]
    public async Task Flush_writes_pending_debounced_save()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var library = new GameLibrary(paths);
        library.AddRange(new[] { new Game { Name = "Pending" } }); // schedules a save

        await library.FlushAsync();

        Assert.True(File.Exists(paths.LibraryFile));
    }

    [Fact]
    public async Task Settings_have_sensible_defaults_and_round_trip()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var settings = new SettingsService(paths);
        await settings.LoadAsync();

        Assert.True(settings.Current.RescanOnStartup);
        Assert.Equal(GameSourceInfo.Launchers.Count, settings.Current.EnabledLaunchers.Count);

        settings.Current.ScanFolders.Add(@"D:\Games");
        settings.Current.SteamGridDbApiKey = "abc";
        settings.Current.SortOrder = SortOrder.Playtime;
        await settings.SaveAsync();

        var again = new SettingsService(paths);
        await again.LoadAsync();
        Assert.Equal(new[] { @"D:\Games" }, again.Current.ScanFolders);
        Assert.Equal("abc", again.Current.SteamGridDbApiKey);
        Assert.Equal(SortOrder.Playtime, again.Current.SortOrder);
    }

    [Fact]
    public void Sample_data_is_tagged_and_valid()
    {
        var games = SampleData.Create();
        Assert.NotEmpty(games);
        Assert.All(games, g => Assert.True(SampleData.IsSample(g)));
        Assert.All(games, g => Assert.False(string.IsNullOrWhiteSpace(g.Name)));
        Assert.Equal(games.Count, games.Select(g => g.Id).Distinct().Count());
    }
}
