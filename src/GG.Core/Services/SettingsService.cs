using GG.Core.Models;
using GG.Core.Storage;

namespace GG.Core.Services;

/// <summary>Holds the current <see cref="AppSettings"/> and saves them to settings.json.</summary>
public sealed class SettingsService
{
    private readonly JsonFileStore<AppSettings> _store;
    private readonly Debouncer _saveDebouncer;

    public SettingsService(AppPaths paths)
    {
        _store = new JsonFileStore<AppSettings>(paths.SettingsFile);
        _saveDebouncer = new Debouncer(SaveAsync, TimeSpan.FromMilliseconds(500));
    }

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Current = await _store.LoadAsync(ct).ConfigureAwait(false);
        Current.ScanFolders ??= new();
        Current.EnabledLaunchers ??= new(GameSourceInfo.Launchers);
    }

    public Task SaveAsync() => _store.SaveAsync(Current);

    public void RequestSave() => _saveDebouncer.Trigger();

    public Task FlushAsync() => _saveDebouncer.FlushAsync();
}
