using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "snipshelf-tests",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private SettingsService CreateService() => new(_folder);

    [Fact]
    public void Current_BeforeLoad_ReturnsDefaults()
    {
        var service = CreateService();

        Assert.Equal(AppTheme.System, service.Current.Theme);
        Assert.Equal(ShellPage.Vault, service.Current.LastPage);
    }

    // Launching the app must not write to disk; a read-only data folder is not a crash.
    [Fact]
    public async Task LoadAsync_NoFile_ReturnsDefaultsWithoutCreatingAnything()
    {
        var service = CreateService();

        var settings = await service.LoadAsync();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.False(File.Exists(service.SettingsFilePath));
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public async Task UpdateAsync_PersistsAcrossInstances()
    {
        var writer = CreateService();
        await writer.UpdateAsync(s =>
        {
            s.Theme = AppTheme.Dark;
            s.LastPage = ShellPage.Favorites;
        });

        var reader = CreateService();
        var settings = await reader.LoadAsync();

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(ShellPage.Favorites, settings.LastPage);
        Assert.Equal(AppTheme.Dark, reader.Current.Theme);
    }

    [Fact]
    public async Task UpdateAsync_RaisesChangedOnce()
    {
        var service = CreateService();
        var raised = 0;
        AppSettings? payload = null;
        service.Changed += (_, s) =>
        {
            raised++;
            payload = s;
        };

        await service.UpdateAsync(s => s.Theme = AppTheme.Light);

        Assert.Equal(1, raised);
        Assert.Equal(AppTheme.Light, payload?.Theme);
    }

    [Fact]
    public async Task UpdateAsync_LeavesNoTemporaryFileBehind()
    {
        var service = CreateService();

        await service.UpdateAsync(s => s.Theme = AppTheme.Dark);

        var files = Directory.GetFiles(_folder).Select(f => Path.GetFileName(f)!).ToArray();
        Assert.Equal(["settings.json"], files);
    }

    // A truncated or hand-mangled file falls back to defaults, but we never delete it:
    // the user may want to recover it.
    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsDefaultsAndKeepsTheFile()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "settings.json");
        await File.WriteAllTextAsync(path, "{\"theme\": ");

        var service = CreateService();
        var settings = await service.LoadAsync();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task UpdateAsync_AfterCorruptLoad_RewritesAValidFile()
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(Path.Combine(_folder, "settings.json"), "garbage");

        var service = CreateService();
        await service.LoadAsync();
        await service.UpdateAsync(s => s.LastPage = ShellPage.Settings);

        var reloaded = await CreateService().LoadAsync();
        Assert.Equal(ShellPage.Settings, reloaded.LastPage);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentWrites_NeverTearTheFile()
    {
        var service = CreateService();
        var pages = Enum.GetValues<ShellPage>();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            service.UpdateAsync(s => s.LastPage = pages[i % pages.Length])));

        var json = await File.ReadAllTextAsync(service.SettingsFilePath);
        Assert.True(AppSettingsSerializer.TryDeserialize(json, out var onDisk));
        Assert.Equal(service.Current.LastPage, onDisk.LastPage);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotHandOutTheLiveInstanceToCallers()
    {
        var service = CreateService();
        await service.UpdateAsync(s => s.Theme = AppTheme.Dark);

        var snapshot = service.Current;
        snapshot.Theme = AppTheme.Light;

        Assert.Equal(AppTheme.Dark, service.Current.Theme);
    }
}
