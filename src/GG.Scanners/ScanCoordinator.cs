using GG.Core.Logging;
using GG.Core.Models;

namespace GG.Scanners;

/// <summary>Runs every scanner in parallel in the background. A scanner that throws is logged and skipped.</summary>
public sealed class ScanCoordinator
{
    private readonly IReadOnlyList<IGameScanner> _scanners;

    public ScanCoordinator(IEnumerable<IGameScanner> scanners)
    {
        _scanners = scanners.ToList();
    }

    /// <summary>The scanners GG ships with (more are added in later steps).</summary>
    public static ScanCoordinator CreateDefault() => new(new IGameScanner[]
    {
        new ShortcutScanner(),
    });

    public async Task<IReadOnlyList<ScanResult>> ScanAsync(ScanContext context, CancellationToken cancellationToken = default)
    {
        var tasks = _scanners.Select(scanner => RunSafeAsync(scanner, context, cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<ScanResult> RunSafeAsync(IGameScanner scanner, ScanContext context, CancellationToken ct)
    {
        try
        {
            return await Task.Run(() => scanner.ScanAsync(context, ct), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error($"{scanner.Source} scanner failed", ex);
            return ScanResult.NotAvailable(scanner.Source, $"{scanner.Source.DisplayName()} scan failed: {ex.Message}");
        }
    }
}
