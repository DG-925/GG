using GG.Core.Logging;
using GG.Core.Models;
using GG.Core.Storage;

namespace GG.Core.Services;

/// <summary>
/// The in-memory game library plus its persistence to library.json.
/// All members are thread-safe; <see cref="Changed"/> may fire on any thread.
/// </summary>
public sealed class GameLibrary
{
    private readonly JsonFileStore<LibraryDocument> _store;
    private readonly Debouncer _saveDebouncer;
    private readonly object _sync = new();
    private List<Game> _games = new();

    public GameLibrary(AppPaths paths)
    {
        _store = new JsonFileStore<LibraryDocument>(paths.LibraryFile);
        _saveDebouncer = new Debouncer(SaveAsync, TimeSpan.FromMilliseconds(750));
    }

    /// <summary>Raised after games are added or removed (not for edits to a single game).</summary>
    public event EventHandler? Changed;

    /// <summary>True if library.json existed when <see cref="LoadAsync"/> ran.</summary>
    public bool ExistedOnDisk { get; private set; }

    /// <summary>A snapshot of the current games.</summary>
    public IReadOnlyList<Game> Games
    {
        get
        {
            lock (_sync)
                return _games.ToList();
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        ExistedOnDisk = _store.Exists;
        var doc = await _store.LoadAsync(ct).ConfigureAwait(false);

        // Drop obviously broken entries rather than failing the whole load.
        var games = doc.Games.Where(g => g is not null && !string.IsNullOrWhiteSpace(g.Name)).ToList();
        foreach (var g in games)
        {
            if (string.IsNullOrWhiteSpace(g.Id))
                g.Id = Guid.NewGuid().ToString("N");
        }

        lock (_sync)
            _games = games;

        Log.Info($"Library loaded: {games.Count} games.");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes library.json immediately.</summary>
    public Task SaveAsync()
    {
        LibraryDocument doc;
        lock (_sync)
            doc = new LibraryDocument { Games = _games.ToList() };
        return _store.SaveAsync(doc);
    }

    /// <summary>Asks for a save in a moment; many quick calls result in one write.</summary>
    public void RequestSave() => _saveDebouncer.Trigger();

    /// <summary>Writes any pending save (call on exit).</summary>
    public Task FlushAsync() => _saveDebouncer.FlushAsync();

    public Game? Find(string id)
    {
        lock (_sync)
            return _games.FirstOrDefault(g => g.Id == id);
    }

    public void AddRange(IEnumerable<Game> games)
    {
        lock (_sync)
            _games.AddRange(games);
        Changed?.Invoke(this, EventArgs.Empty);
        RequestSave();
    }

    /// <summary>Replaces the whole library (after a scan merge) and saves soon.</summary>
    public void ReplaceAll(IEnumerable<Game> games)
    {
        lock (_sync)
            _games = games.ToList();
        Changed?.Invoke(this, EventArgs.Empty);
        RequestSave();
    }

    public void Remove(string id)
    {
        lock (_sync)
            _games.RemoveAll(g => g.Id == id);
        Changed?.Invoke(this, EventArgs.Empty);
        RequestSave();
    }

    /// <summary>Removes every game matching <paramref name="predicate"/>. Returns how many were removed.</summary>
    public int RemoveWhere(Func<Game, bool> predicate)
    {
        int removed;
        lock (_sync)
            removed = _games.RemoveAll(g => predicate(g));
        if (removed > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
            RequestSave();
        }
        return removed;
    }
}
