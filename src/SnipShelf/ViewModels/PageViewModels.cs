using CommunityToolkit.Mvvm.ComponentModel;
using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.ViewModels;

public sealed class VaultViewModel : ViewModelBase
{
    public string Title => "Vault";
}

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
        _theme = settings.Load().Theme;
    }

    // Theme application itself lives in App (it owns RequestedThemeVariant); this page
    // only persists the choice.
    partial void OnThemeChanged(AppTheme value)
    {
        _ = _settings.UpdateAsync(s => s.Theme = value);
    }
}
