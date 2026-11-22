using GG.Core.Models;
using GG.Core.Services;

namespace GG.Tests;

public class LibraryMergerTests
{
    private static readonly IReadOnlySet<GameSource> Shortcuts = new HashSet<GameSource> { GameSource.Shortcut };
    private static readonly IReadOnlySet<GameSource> ShortcutsAndSteam = new HashSet<GameSource> { GameSource.Shortcut, GameSource.Steam };

    private static Game Shortcut(string name, string exe, string? lnk = null) =>
        new()
        {
            Name = name,
            Source = GameSource.Shortcut,
            SourceId = lnk ?? $@"C:\Shortcuts\{name}.lnk",
            ExePath = exe,
            InstallPath = Path.GetDirectoryName(exe.Replace('\\', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '\\'),
        };

    private static Game Steam(string name, string appId, string installDir) =>
        new()
        {
            Name = name,
            Source = GameSource.Steam,
            SourceId = appId,
            InstallPath = installDir,
            LaunchUri = $"steam://rungameid/{appId}",
        };

    [Fact]
    public void New_games_are_added()
    {
        var result = LibraryMerger.Merge(Array.Empty<Game>(), new[] { Shortcut("Celeste", @"D:\Games\Celeste\Celeste.exe") }, Shortcuts);

        Assert.Single(result.Games);
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public void Rescan_keeps_id_favorite_and_playtime()
    {
        var first = LibraryMerger.Merge(Array.Empty<Game>(), new[] { Shortcut("Celeste", @"D:\Games\Celeste\Celeste.exe") }, Shortcuts).Games;
        first[0].IsFavorite = true;
        first[0].TotalPlaytime = TimeSpan.FromHours(3);
        var id = first[0].Id;

        var second = LibraryMerger.Merge(first, new[] { Shortcut("Celeste", @"D:\Games\Celeste\Celeste.exe") }, Shortcuts);

        var game = Assert.Single(second.Games);
        Assert.Equal(id, game.Id);
        Assert.True(game.IsFavorite);
        Assert.Equal(TimeSpan.FromHours(3), game.TotalPlaytime);
        Assert.Equal(0, second.Added);
    }

    [Fact]
    public void Shortcut_exe_wins_over_launcher_when_same_game()
    {
        var scanned = new[]
        {
            Steam("Hollow Knight", "367520", @"C:\Steam\steamapps\common\Hollow Knight"),
            Shortcut("Hollow Knight", @"C:\Steam\steamapps\common\Hollow Knight\hollow_knight.exe"),
        };

        var result = LibraryMerger.Merge(Array.Empty<Game>(), scanned, ShortcutsAndSteam);

        var game = Assert.Single(result.Games);
        Assert.Equal(GameSource.Shortcut, game.Source);
        Assert.Equal(@"C:\Steam\steamapps\common\Hollow Knight\hollow_knight.exe", game.ExePath);
        Assert.Equal("367520", game.GetExternalId(GameSource.Steam)); // Steam id kept for covers
    }

    [Fact]
    public void Launcher_and_shortcut_merge_by_normalized_name()
    {
        var scanned = new[]
        {
            Steam("The Witcher® 3: Wild Hunt", "292030", @"E:\SteamLibrary\steamapps\common\The Witcher 3"),
            new Game { Name = "the witcher 3 wild hunt", Source = GameSource.Shortcut, SourceId = @"C:\s\w3.url", LaunchUri = "steam://rungameid/292030" },
        };

        var result = LibraryMerger.Merge(Array.Empty<Game>(), scanned, ShortcutsAndSteam);

        var game = Assert.Single(result.Games);
        Assert.Equal(GameSource.Shortcut, game.Source);
        Assert.Equal(@"E:\SteamLibrary\steamapps\common\The Witcher 3", game.InstallPath); // gap filled from Steam
    }

    [Fact]
    public void Two_shortcuts_with_the_same_exe_folder_stay_separate()
    {
        // e.g. two emulator shortcuts with different arguments
        var scanned = new[]
        {
            Shortcut("Mario Kart", @"D:\Emu\Dolphin\Dolphin.exe"),
            Shortcut("Zelda", @"D:\Emu\Dolphin\Dolphin.exe"),
        };

        var result = LibraryMerger.Merge(Array.Empty<Game>(), scanned, Shortcuts);

        Assert.Equal(2, result.Games.Count);
    }

    [Fact]
    public void User_chosen_exe_is_never_overwritten()
    {
        var existing = Shortcut("Doom", @"D:\Games\Doom\Doom.exe");
        existing.SetExternalId(GameSource.Shortcut, existing.SourceId);
        existing.ExePath = @"D:\Games\Doom\DoomX64.exe";
        existing.UserOverrodeExe = true;

        var result = LibraryMerger.Merge(new[] { existing }, new[] { Shortcut("Doom", @"D:\Games\Doom\Doom.exe") }, Shortcuts);

        Assert.Equal(@"D:\Games\Doom\DoomX64.exe", Assert.Single(result.Games).ExePath);
    }

    [Fact]
    public void User_rename_is_kept()
    {
        var existing = Shortcut("Doom", @"D:\Games\Doom\Doom.exe");
        existing.SetExternalId(GameSource.Shortcut, existing.SourceId);
        existing.Name = "DOOM (1993)";
        existing.UserRenamed = true;

        var result = LibraryMerger.Merge(new[] { existing }, new[] { Shortcut("Doom", @"D:\Games\Doom\Doom.exe") }, Shortcuts);

        Assert.Equal("DOOM (1993)", Assert.Single(result.Games).Name);
    }

    [Fact]
    public void Deleted_shortcuts_are_removed_unless_favorite_or_played()
    {
        var plain = Shortcut("Gone", @"D:\Games\Gone\Gone.exe");
        var favorite = Shortcut("Loved", @"D:\Games\Loved\Loved.exe");
        favorite.IsFavorite = true;
        var played = Shortcut("Played", @"D:\Games\Played\Played.exe");
        played.TotalPlaytime = TimeSpan.FromMinutes(30);
        foreach (var g in new[] { plain, favorite, played })
            g.SetExternalId(GameSource.Shortcut, g.SourceId);

        var result = LibraryMerger.Merge(new[] { plain, favorite, played }, Array.Empty<Game>(), Shortcuts);

        Assert.Equal(new[] { "Loved", "Played" }, result.Games.Select(g => g.Name).OrderBy(n => n));
        Assert.Equal(1, result.Removed);
    }

    [Fact]
    public void Games_of_sources_not_scanned_are_left_alone()
    {
        var steamGame = Steam("Portal", "400", @"C:\Steam\steamapps\common\Portal");
        steamGame.SetExternalId(GameSource.Steam, "400");

        var result = LibraryMerger.Merge(new[] { steamGame }, Array.Empty<Game>(), Shortcuts);

        Assert.Single(result.Games);
    }

    [Fact]
    public void Sample_games_are_dropped_on_first_real_scan()
    {
        var result = LibraryMerger.Merge(SampleData.Create(), new[] { Shortcut("Celeste", @"D:\Games\Celeste\Celeste.exe") }, Shortcuts);

        Assert.Equal("Celeste", Assert.Single(result.Games).Name);
    }

    [Theory]
    [InlineData("The Witcher® 3: Wild Hunt", "thewitcher3wildhunt")]
    [InlineData("Ratchet & Clank", "ratchetandclank")]
    [InlineData("  DOOM Eternal  ", "doometernal")]
    public void Names_are_normalized(string input, string expected) =>
        Assert.Equal(expected, GameMatching.NormalizeName(input));

    [Fact]
    public void Drive_roots_never_count_as_a_game_folder()
    {
        Assert.False(GameMatching.IsInside(@"D:\game.exe", @"D:\"));
        Assert.False(GameMatching.IsInside(@"D:\Games\X\x.exe", @"D:\Games"));
        Assert.True(GameMatching.IsInside(@"D:\Games\X\bin\x.exe", @"D:\Games\X"));
    }
}
