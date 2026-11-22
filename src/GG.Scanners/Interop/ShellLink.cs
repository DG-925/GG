using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace GG.Scanners.Interop;

/// <summary>What a .lnk file points to.</summary>
public sealed record ShortcutInfo(string TargetPath, string Arguments, string WorkingDirectory, string IconLocation, int IconIndex);

/// <summary>
/// Reads (and, for tests, writes) Windows .lnk files through the shell's IShellLink COM object.
/// No admin rights needed, and it does not try to "repair" broken shortcuts.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShellLink
{
    private const int MaxPath = 4096;

    /// <summary>Reads a .lnk file. TargetPath is empty for shortcuts with no file target (Store apps, special folders).</summary>
    public static ShortcutInfo Read(string lnkPath)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        try
        {
            ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);

            var target = new StringBuilder(MaxPath);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);

            var args = new StringBuilder(MaxPath);
            link.GetArguments(args, args.Capacity);

            var dir = new StringBuilder(MaxPath);
            link.GetWorkingDirectory(dir, dir.Capacity);

            var icon = new StringBuilder(MaxPath);
            link.GetIconLocation(icon, icon.Capacity, out var iconIndex);

            return new ShortcutInfo(
                Environment.ExpandEnvironmentVariables(target.ToString()),
                args.ToString(),
                Environment.ExpandEnvironmentVariables(dir.ToString()),
                Environment.ExpandEnvironmentVariables(icon.ToString()),
                iconIndex);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>Creates a .lnk file (used by tests).</summary>
    public static void Create(string lnkPath, string targetPath, string arguments = "", string workingDirectory = "")
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        try
        {
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
