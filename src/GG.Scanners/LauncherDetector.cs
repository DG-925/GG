using GG.Core.Logging;
using GG.Core.Models;
using Microsoft.Win32;

namespace GG.Scanners;

/// <summary>
/// Works out which game launchers are actually installed on this PC, from their registry entries
/// and install folders. The sidebar only lists installed launchers, and the scanners skip the rest.
/// Every check is wrapped so a missing key or odd install can never crash the app.
/// </summary>
public static class LauncherDetector
{
    /// <summary>The set of installed launchers (empty on non-Windows systems).</summary>
    public static IReadOnlySet<GameSource> DetectInstalled()
    {
        var installed = new HashSet<GameSource>();
        if (!OperatingSystem.IsWindows())
            return installed;

        foreach (var launcher in GameSourceInfo.Launchers)
        {
            bool found;
            try
            {
                found = IsInstalled(launcher);
            }
            catch (Exception ex)
            {
                Log.Warn($"Launcher check for {launcher} failed: {ex.Message}");
                found = false;
            }

            if (found)
                installed.Add(launcher);
        }

        Log.Info("Installed launchers: " + (installed.Count == 0 ? "none" : string.Join(", ", installed)));
        return installed;
    }

    /// <summary>Is <paramref name="launcher"/> installed? Always false for non-launcher sources.</summary>
    public static bool IsInstalled(GameSource launcher)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        return launcher switch
        {
            GameSource.Steam =>
                DirectoryFromRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")
                || DirectoryFromRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),

            GameSource.Epic =>
                Directory.Exists(Path.Combine(ProgramData, @"Epic\EpicGamesLauncher\Data\Manifests"))
                || KeyExists(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher"),

            // The Xbox app needs the Gaming Services package, which leaves this key behind.
            // Games installed through it land in X:\XboxGames folders.
            GameSource.Xbox =>
                KeyExists(Registry.LocalMachine, @"SOFTWARE\Microsoft\GamingServices")
                || XboxGamesFolderExists(),

            // GOG games can be installed without Galaxy, so either the client or any game counts.
            GameSource.GOG =>
                KeyExists(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient")
                || HasSubKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\GOG.com\Games"),

            GameSource.EA =>
                KeyExists(Registry.LocalMachine, @"SOFTWARE\Electronic Arts\EA Desktop")
                || KeyExists(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Origin"),

            GameSource.Ubisoft =>
                DirectoryFromRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Ubisoft\Launcher", "InstallDir"),

            GameSource.BattleNet =>
                KeyExists(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Blizzard Entertainment\Battle.net")
                || File.Exists(Path.Combine(ProgramFilesX86, @"Battle.net\Battle.net.exe")),

            _ => false,
        };
    }

    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static string ProgramFilesX86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    private static bool KeyExists(RegistryKey root, string path)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        using var key = root.OpenSubKey(path);
        return key is not null;
    }

    private static bool HasSubKeys(RegistryKey root, string path)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        using var key = root.OpenSubKey(path);
        return key is not null && key.SubKeyCount > 0;
    }

    /// <summary>True if the registry value holds a folder that still exists (uninstalls often leave the key).</summary>
    private static bool DirectoryFromRegistry(RegistryKey root, string path, string valueName)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        using var key = root.OpenSubKey(path);
        return key?.GetValue(valueName) is string dir
            && !string.IsNullOrWhiteSpace(dir)
            && Directory.Exists(dir.Replace('/', '\\'));
    }

    private static bool XboxGamesFolderExists()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady
                    && Directory.Exists(Path.Combine(drive.RootDirectory.FullName, "XboxGames")))
                    return true;
            }
            catch (IOException)
            {
                // Drive went away or isn't readable; ignore.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return false;
    }
}
