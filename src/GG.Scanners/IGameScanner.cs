using GG.Core.Models;

namespace GG.Scanners;

/// <summary>
/// One source of installed games (a launcher, a shortcut folder, a games folder...).
/// Implementations must fail safely: if the launcher is not installed they return
/// <see cref="ScanResult.NotAvailable"/> and log, they never throw for "expected" problems.
/// </summary>
public interface IGameScanner
{
    /// <summary>The source every game from this scanner is tagged with.</summary>
    GameSource Source { get; }

    /// <summary>Finds games. Runs on a background thread.</summary>
    Task<ScanResult> ScanAsync(ScanContext context, CancellationToken cancellationToken);
}

/// <summary>Input shared by all scanners.</summary>
/// <param name="Settings">Current settings (scan folders, shortcut folder...).</param>
/// <param name="Progress">Optional status text for the progress display.</param>
public sealed record ScanContext(AppSettings Settings, IProgress<string>? Progress = null);

/// <summary>What a scanner found.</summary>
public sealed record ScanResult(GameSource Source, IReadOnlyList<Game> Games, bool SourceAvailable, string? Message = null)
{
    public static ScanResult NotAvailable(GameSource source, string reason) =>
        new(source, Array.Empty<Game>(), false, reason);

    public static ScanResult Found(GameSource source, IReadOnlyList<Game> games) =>
        new(source, games, true);
}
