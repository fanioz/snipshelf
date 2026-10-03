using SnipShelf.Models;

namespace SnipShelf.Services;

/// <inheritdoc cref="ISettingsService"/>
public sealed class SettingsService : ISettingsService
{
    private const string FileName = "settings.json";

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private AppSettings _current = new();

    public SettingsService(string dataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
        DataFolder = dataFolder;
        SettingsFilePath = Path.Combine(dataFolder, FileName);
    }

    public string DataFolder { get; }

    public string SettingsFilePath { get; }

    public AppSettings Current => Volatile.Read(ref _current).Clone();

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Load()
    {
        var loaded = ReadFile() ?? new AppSettings();
        Volatile.Write(ref _current, loaded);
        return loaded.Clone();
    }

    /// <inheritdoc cref="ISettingsService.Load"/>
    public Task<AppSettings> LoadAsync() => Task.FromResult(Load());

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = Volatile.Read(ref _current).Clone();
            mutate(updated);
            Volatile.Write(ref _current, updated);

            await WriteFileAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }

        Changed?.Invoke(this, Volatile.Read(ref _current).Clone());
    }

    private AppSettings? ReadFile()
    {
        try
        {
            return File.Exists(SettingsFilePath)
                ? (AppSettingsSerializer.TryDeserialize(File.ReadAllText(SettingsFilePath), out var parsed)
                    ? parsed
                    : null)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable settings file is not worth refusing to start over.
            return null;
        }
    }

    // Write to a sibling temp file and move it into place, so a crash or a full disk
    // cannot leave a half-written settings.json behind.
    private async Task WriteFileAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var temp = SettingsFilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(DataFolder);
            await File.WriteAllTextAsync(temp, AppSettingsSerializer.Serialize(settings), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temp, SettingsFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemp(temp);
        }
    }

    private static void TryDeleteTemp(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing useful left to do; the stale temp file is harmless.
        }
    }
}
