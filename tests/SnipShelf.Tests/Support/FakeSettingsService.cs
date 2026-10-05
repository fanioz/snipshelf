using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.Tests.Support;

/// <summary>In-memory settings: no disk, records every mutation for assertions.</summary>
public sealed class FakeSettingsService : ISettingsService
{
    private AppSettings _current;

    public FakeSettingsService(AppSettings? initial = null)
    {
        _current = (initial ?? new AppSettings()).Clone();
    }

    /// <summary>A snapshot of the current settings. Mutating it has no effect.</summary>
    public AppSettings Current => _current.Clone();

    public string DataFolder => "/fake/data";

    public string SettingsFilePath => "/fake/data/settings.json";

    public event EventHandler<AppSettings>? Changed;

    public List<Action<AppSettings>> Updates { get; } = [];

    public int LoadCalls { get; private set; }

    public AppSettings Load()
    {
        LoadCalls++;
        return Current.Clone();
    }

    public Task<AppSettings> LoadAsync() => Task.FromResult(Load());

    public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Updates.Add(mutate);
        var updated = _current.Clone();
        mutate(updated);
        _current = updated;
        Changed?.Invoke(this, Current);
        return Task.CompletedTask;
    }
}
