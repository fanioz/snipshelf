using CommunityToolkit.Mvvm.ComponentModel;
using SnipShelf.Models;
using SnipShelf.Services;
using System.Diagnostics;

namespace SnipShelf.ViewModels;

public sealed class FavoritesViewModel : ViewModelBase
{
    public string Title => "Favorites";
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    [ObservableProperty]
    private AppTheme _theme;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        _theme = settings.Current.Theme;
    }

    // Theme application itself lives in App (it owns RequestedThemeVariant); this page
    // only persists the choice.
    partial void OnThemeChanged(AppTheme value)
    {
        _ = PersistThemeAsync(value);
    }

    // UpdateAsync throws on a failed write (disk full, permissions); this transient VM
    // must observe that instead of crashing or leaking an unobserved task. The in-memory
    // snapshot keeps the choice either way.
    private async Task PersistThemeAsync(AppTheme value)
    {
        try
        {
            await _settings.UpdateAsync(s => s.Theme = value);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal end for the write.
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to persist theme settings: {ex.Message}");
        }
    }
}
