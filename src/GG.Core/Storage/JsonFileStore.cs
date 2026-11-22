using System.Text.Json;
using GG.Core.Logging;

namespace GG.Core.Storage;

/// <summary>
/// Loads and saves one JSON document safely:
/// <list type="bullet">
/// <item>saves go to a temp file first and then replace the real file, so a crash never leaves half a file;</item>
/// <item>the previous version is kept as *.bak;</item>
/// <item>a corrupt file is moved aside (*.corrupt-yyyyMMdd-HHmmss.json) and the backup is tried instead.</item>
/// </list>
/// </summary>
public sealed class JsonFileStore<T> where T : class, new()
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileStore(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public string BackupPath => FilePath + ".bak";

    /// <summary>True if the file exists on disk.</summary>
    public bool Exists => File.Exists(FilePath);

    /// <summary>Reads the document, or returns a new empty one if it is missing or unreadable.</summary>
    public async Task<T> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
                return new T();

            var result = await TryReadAsync(FilePath, ct).ConfigureAwait(false);
            if (result is not null)
                return result;

            // The main file is broken: keep a copy for inspection, then fall back to the backup.
            QuarantineCorruptFile();
            if (File.Exists(BackupPath))
            {
                var backup = await TryReadAsync(BackupPath, ct).ConfigureAwait(false);
                if (backup is not null)
                {
                    Log.Warn($"Recovered {Path.GetFileName(FilePath)} from its backup.");
                    return backup;
                }
            }

            return new T();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Writes the document atomically.</summary>
    public async Task SaveAsync(T value, CancellationToken ct = default)
    {
        // Serialize outside the lock-protected file work; callers should not mutate during a save.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Options);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = FilePath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(FilePath))
                File.Replace(tempPath, FilePath, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, FilePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<T?> TryReadAsync(string path, CancellationToken ct)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            Log.Error($"Could not parse {path}", ex);
            return null;
        }
        catch (IOException ex)
        {
            Log.Error($"Could not read {path}", ex);
            return null;
        }
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            var target = Path.ChangeExtension(FilePath, null) + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            File.Copy(FilePath, target, overwrite: true);
            Log.Warn($"Moved unreadable {Path.GetFileName(FilePath)} aside to {target}");
        }
        catch (Exception ex)
        {
            Log.Error("Could not back up corrupt file", ex);
        }
    }
}
