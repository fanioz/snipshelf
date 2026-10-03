using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.Tests.Support;

/// <summary>In-memory settings: no disk, records every mutation for assertions.</summary>
public sealed class FakeSettingsService : ISettingsService
{
    public FakeSettingsService(AppSettings? initial = null)
    {
        Current = initial ?? new AppSettings();
    }

    public AppSettings Current { get; private set; }

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
        Updates.Add(mutate);
        var updated = Current.Clone();
        mutate(updated);
        Current = updated;
        Changed?.Invoke(this, Current.Clone());
        return Task.CompletedTask;
    }
}
