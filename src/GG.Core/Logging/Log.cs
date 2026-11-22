using System.Diagnostics;

namespace GG.Core.Logging;

/// <summary>
/// Tiny thread-safe file logger (%AppData%\GG\logs\gg.log). Scanners log here when a launcher is
/// missing instead of throwing. Rolls over to gg.old.log at 2 MB. Never throws.
/// </summary>
public static class Log
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();
    private static string? _path;

    /// <summary>Call once at startup. Until then messages only go to the debugger output.</summary>
    public static void Initialize(string logFilePath)
    {
        lock (Sync)
        {
            _path = logFilePath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
                var info = new FileInfo(logFilePath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var old = Path.Combine(info.DirectoryName!, "gg.old.log");
                    File.Copy(logFilePath, old, overwrite: true);
                    File.Delete(logFilePath);
                }
            }
            catch
            {
                // Logging must never break the app.
            }
        }
    }

    public static void Info(string message) => Write("INFO ", message, null);

    public static void Warn(string message) => Write("WARN ", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}";
        if (ex is not null)
            line += Environment.NewLine + "    " + ex;

        Debug.WriteLine(line);

        lock (Sync)
        {
            if (_path is null)
                return;
            try
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch
            {
                // Ignore: disk full, file locked, etc.
            }
        }
    }
}
