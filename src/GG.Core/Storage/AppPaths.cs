namespace GG.Core.Storage;

/// <summary>
/// All on-disk locations used by GG. Everything lives under %AppData%\GG so no admin rights are needed.
/// Tests pass their own root folder.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string root)
    {
        Root = root;
    }

    /// <summary>%AppData%\GG</summary>
    public static AppPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GG"));

    public string Root { get; }

    public string LibraryFile => Path.Combine(Root, "library.json");

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string CacheDir => Path.Combine(Root, "cache");

    public string CoversDir => Path.Combine(CacheDir, "covers");

    public string IconsDir => Path.Combine(CacheDir, "icons");

    public string LogsDir => Path.Combine(Root, "logs");

    public string LogFile => Path.Combine(LogsDir, "gg.log");

    /// <summary>Creates every folder if it does not exist yet.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(CoversDir);
        Directory.CreateDirectory(IconsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
