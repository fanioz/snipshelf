using Avalonia.Controls;
using SnipShelf.Models;
using SnipShelf.Navigation;
using SnipShelf.ViewModels;

namespace SnipShelf.Tests.Navigation;

public class ShellPageMapTests
{
    // Adding a ShellPage without wiring a view model would otherwise fail at runtime
    // the first time somebody clicks the nav item.
    [Fact]
    public void EveryShellPageHasADefinition()
    {
        foreach (var page in Enum.GetValues<ShellPage>())
        {
            var definition = ShellPageMap.For(page);
            Assert.Equal(page, definition.Page);
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
        }
    }

    [Fact]
    public void DefinitionsAreOrderedForTheNavigationPane()
    {
        Assert.Equal(
            [ShellPage.Vault, ShellPage.Favorites, ShellPage.Settings],
            ShellPageMap.Definitions.Select(d => d.Page));
    }

    [Fact]
    public void ViewModelTypesAreDistinctAndDeriveFromViewModelBase()
    {
        var viewModelTypes = ShellPageMap.Definitions.Select(d => d.ViewModelType).ToArray();

        Assert.Equal(viewModelTypes.Length, viewModelTypes.Distinct().Count());
        Assert.All(viewModelTypes, t => Assert.True(typeof(ViewModelBase).IsAssignableFrom(t)));
    }

    [Fact]
    public void ViewTypesAreDistinctAndAreControls()
    {
        var viewTypes = ShellPageMap.Definitions.Select(d => d.ViewType).ToArray();

        Assert.Equal(viewTypes.Length, viewTypes.Distinct().Count());
        Assert.All(viewTypes, t => Assert.True(typeof(Control).IsAssignableFrom(t)));
    }

    [Fact]
    public void ViewTypesHaveAParameterlessConstructor_SoTheFrameCanBuildThem()
    {
        Assert.All(
            ShellPageMap.Definitions.Select(d => d.ViewType),
            t => Assert.NotNull(t.GetConstructor(Type.EmptyTypes)));
    }

    [Fact]
    public void TryGetByViewModelType_RoundTripsEveryDefinition()
    {
        foreach (var definition in ShellPageMap.Definitions)
        {
            Assert.True(ShellPageMap.TryGetByViewModelType(definition.ViewModelType, out var found));
            Assert.Equal(definition.Page, found.Page);
        }
    }

    [Fact]
    public void TryGetByViewModelType_UnknownTypeReturnsFalse()
    {
        Assert.False(ShellPageMap.TryGetByViewModelType(typeof(string), out _));
    }

    [Fact]
    public void For_UnknownPageThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShellPageMap.For((ShellPage)99));
    }
}
