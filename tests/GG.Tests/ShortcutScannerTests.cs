using GG.Core.Models;
using GG.Core.Services;
using GG.Scanners;
using GG.Scanners.Interop;

namespace GG.Tests;

public class ShortcutScannerTests
{
    private static string WriteUrl(TempDir dir, string fileName, string url)
    {
        var path = dir.Combine(fileName);
        File.WriteAllText(path, $"[InternetShortcut]\r\nURL={url}\r\nIconIndex=0\r\n");
        return path;
    }

    [Fact]
    public void Steam_url_shortcut_becomes_a_game_with_steam_id()
    {
        using var dir = new TempDir();
        WriteUrl(dir, "Hollow Knight.url", "steam://rungameid/367520");

        var result = new ShortcutScanner().Scan(dir.Path);

        var game = Assert.Single(result.Games);
        Assert.Equal("Hollow Knight", game.Name);
        Assert.Equal(GameSource.Shortcut, game.Source);
        Assert.Equal("steam://rungameid/367520", game.LaunchUri);
        Assert.Equal("367520", game.GetExternalId(GameSource.Steam));
    }

    [Fact]
    public void Epic_url_shortcut_keeps_the_app_name()
    {
        using var dir = new TempDir();
        WriteUrl(dir, "Fortnite.url", "com.epicgames.launcher://apps/fn%3A4fe75bbc5a674f4f9b356b5c90567da5%3AFortnite?action=launch&silent=true");

        var game = Assert.Single(new ShortcutScanner().Scan(dir.Path).Games);

        Assert.Equal("Fortnite", game.GetExternalId(GameSource.Epic));
    }

    [Fact]
    public void Web_links_and_uninstallers_are_skipped()
    {
        using var dir = new TempDir();
        WriteUrl(dir, "Visit our website.url", "https://example.com");
        WriteUrl(dir, "Some Game.url", "https://store.example.com/game");
        WriteUrl(dir, "Uninstall Some Game.url", "steam://uninstall/123");

        Assert.Empty(new ShortcutScanner().Scan(dir.Path).Games);
    }

    [Fact]
    public void Subfolders_are_included_and_shortcut_suffix_removed()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("RPG"));
        WriteUrl(dir, Path.Combine("RPG", "Elden Ring - Shortcut.url"), "steam://rungameid/1245620");

        var game = Assert.Single(new ShortcutScanner().Scan(dir.Path).Games);

        Assert.Equal("Elden Ring", game.Name);
    }

    [Fact]
    public void Missing_folder_is_reported_not_thrown()
    {
        var result = new ShortcutScanner().Scan(Path.Combine(Path.GetTempPath(), "gg-does-not-exist-" + Guid.NewGuid()));

        Assert.False(result.SourceAvailable);
        Assert.Empty(result.Games);
    }

    [Fact]
    public void Lnk_shortcut_is_resolved_to_exe_arguments_and_folder()
    {
        if (!OperatingSystem.IsWindows())
            return; // .lnk files need the Windows shell; this runs in the Windows CI build.

        using var dir = new TempDir();
        var gameDir = dir.Combine("Games", "Celeste");
        Directory.CreateDirectory(gameDir);
        var exe = Path.Combine(gameDir, "Celeste.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
        var shortcuts = dir.Combine("Shortcuts");
        Directory.CreateDirectory(shortcuts);
        ShellLink.Create(Path.Combine(shortcuts, "Celeste.lnk"), exe, "-windowed", gameDir);

        var game = Assert.Single(new ShortcutScanner().Scan(shortcuts).Games);

        Assert.Equal("Celeste", game.Name);
        Assert.Equal(exe, game.ExePath, ignoreCase: true);
        Assert.Equal("-windowed", game.Arguments);
        Assert.Equal(gameDir, game.WorkingDirectory, ignoreCase: true);
        Assert.Equal(gameDir, game.InstallPath, ignoreCase: true);
    }
}

public class GameLauncherTests
{
    [Fact]
    public void Missing_exe_reports_missing_file()
    {
        var game = new Game { Name = "Ghost", ExePath = Path.Combine(Path.GetTempPath(), "gg-missing-" + Guid.NewGuid() + ".exe") };

        var result = GameLauncher.Launch(game);

        Assert.Equal(LaunchError.MissingFile, result.Error);
    }

    [Fact]
    public void Entry_without_exe_or_uri_reports_nothing_to_launch()
    {
        Assert.Equal(LaunchError.NothingToLaunch, GameLauncher.Launch(new Game { Name = "Empty" }).Error);
    }

    [Fact]
    public void Missing_shortcut_target_reports_missing_file()
    {
        var game = new Game { Name = "Store game", LaunchUri = @"C:\gg-nowhere\" + Guid.NewGuid() + ".lnk" };

        Assert.Equal(LaunchError.MissingFile, GameLauncher.Launch(game).Error);
    }
}
