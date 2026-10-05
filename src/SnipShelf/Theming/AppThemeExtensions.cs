using Avalonia.Styling;
using SnipShelf.Models;

namespace SnipShelf.Theming;

public static class AppThemeExtensions
{
    /// <summary>
    /// Maps the user's theme preference onto an Avalonia <see cref="ThemeVariant"/>.
    /// "System" is not a third palette — it is Avalonia's Default, which follows the OS.
    /// Unknown values (settings.json is hand-editable) degrade to Default, never throw.
    /// </summary>
    public static ThemeVariant ToThemeVariant(this AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
