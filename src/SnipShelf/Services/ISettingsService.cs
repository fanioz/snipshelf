using SnipShelf.Models;

namespace SnipShelf.Services;

public interface ISettingsService
{
    /// <summary>A snapshot of the current settings. Mutating it has no effect.</summary>
    AppSettings Current { get; }

    /// <summary>Folder holding settings.json and (from the data-layer ticket on) the vault database.</summary>
    string DataFolder { get; }

    string SettingsFilePath { get; }

    /// <summary>Raised after <see cref="UpdateAsync"/> persists a change; never raised for a failed write.</summary>
    event EventHandler<AppSettings>? Changed;

    /// <summary>
    /// Reads settings.json. Synchronous on purpose: this runs at startup, before any window
    /// exists — the shell reads the last page from it in its constructor, and the theme has
    /// to be known before the first frame is drawn. Serialized with <see cref="UpdateAsync"/>
    /// so a load never clobbers a pending save.
    /// </summary>
    AppSettings Load();

    /// <summary>Reads settings.json asynchronously, for callers already off the startup path.</summary>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// Applies <paramref name="mutate"/> to a copy of the current settings and writes it out.
    /// The in-memory snapshot is updated even if the write fails, so the user's choice still
    /// holds for the rest of the session; the write itself may throw
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);
}
