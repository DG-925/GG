using GG.Core.Logging;

namespace GG.Core.Services;

/// <summary>
/// Runs an async action once after calls stop coming in for <c>delay</c>.
/// Used to coalesce many small changes (favorite toggles, slider moves) into one save.
/// </summary>
public sealed class Debouncer
{
    private readonly Func<Task> _action;
    private readonly TimeSpan _delay;
    private readonly object _sync = new();
    private CancellationTokenSource? _pending;

    public Debouncer(Func<Task> action, TimeSpan delay)
    {
        _action = action;
        _delay = delay;
    }

    /// <summary>Schedules the action, restarting the timer if it was already scheduled.</summary>
    public void Trigger()
    {
        CancellationTokenSource cts;
        lock (_sync)
        {
            _pending?.Cancel();
            _pending = cts = new CancellationTokenSource();
        }

        _ = RunAfterDelayAsync(cts);
    }

    /// <summary>Runs the action now if one is pending (call on shutdown).</summary>
    public async Task FlushAsync()
    {
        CancellationTokenSource? pending;
        lock (_sync)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending is null)
            return;

        pending.Cancel();
        await _action().ConfigureAwait(false);
    }

    private async Task RunAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_delay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_sync)
        {
            if (!ReferenceEquals(_pending, cts))
                return;
            _pending = null;
        }

        try
        {
            await _action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error("Debounced action failed", ex);
        }
    }
}
