using Avalonia.Styling;
using SnipShelf.Models;
using SnipShelf.Theming;

namespace SnipShelf.Tests.Theming;

public class AppThemeMappingTests
{
    [Theory]
    [InlineData(AppTheme.Light, "Light")]
    [InlineData(AppTheme.Dark, "Dark")]
    public void ToThemeVariant_MapsExplicitThemes(AppTheme theme, string expectedKey)
    {
        Assert.Equal(expectedKey, theme.ToThemeVariant().Key);
    }

    // "System" is not a third palette — it is Avalonia's Default, which follows the OS.
    [Fact]
    public void ToThemeVariant_SystemIsAvaloniaDefault()
    {
        Assert.Same(ThemeVariant.Default, AppTheme.System.ToThemeVariant());
    }

    [Fact]
    public void ToThemeVariant_CoversEveryDeclaredTheme()
    {
        foreach (var theme in Enum.GetValues<AppTheme>())
        {
            Assert.NotNull(theme.ToThemeVariant());
        }
    }

    // Settings.json is user-editable, so an out-of-range value must degrade, not throw.
    [Fact]
    public void ToThemeVariant_UnknownValueFallsBackToDefault()
    {
        Assert.Same(ThemeVariant.Default, ((AppTheme)99).ToThemeVariant());
    }
}
