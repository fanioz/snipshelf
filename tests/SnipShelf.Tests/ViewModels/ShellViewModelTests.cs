using SnipShelf.Models;
using SnipShelf.Navigation;
using SnipShelf.Tests.Support;
using SnipShelf.ViewModels;

namespace SnipShelf.Tests.ViewModels;

public sealed class ShellViewModelTests
{
    private static (ShellViewModel ViewModel, FakeSettingsService Settings, FakeNavigationService Navigation)
        Create(AppSettings? initialSettings = null)
    {
        var settings = new FakeSettingsService(initialSettings);
        var navigation = new FakeNavigationService();
        var viewModel = new ShellViewModel(settings, navigation);
        return (viewModel, settings, navigation);
    }

    [Fact]
    public void Constructor_SelectsTheRestoredPage_NavigatesToItButDoesNotWrite()
    {
        var (viewModel, settings, navigation) = Create(
            new AppSettings { Theme = AppTheme.Dark, LastPage = ShellPage.Favorites });

        Assert.Equal(ShellPage.Favorites, viewModel.SelectedNavItem?.Page);
        // Navigation fires (buffered until the Frame attaches) but nothing changed on
        // disk, so startup must not persist.
        Assert.Equal([ShellPage.Favorites], navigation.Navigations);
        Assert.Empty(settings.Updates);
    }

    [Fact]
    public void Constructor_UnknownLastPage_FallsBackToFirstItem()
    {
        var (viewModel, _, navigation) = Create(new AppSettings { LastPage = (ShellPage)99 });

        Assert.Equal(ShellPage.Vault, viewModel.SelectedNavItem?.Page);
        Assert.Equal([ShellPage.Vault], navigation.Navigations);
    }

    [Fact]
    public void Constructor_ExposesEveryShellPageInPaneOrder()
    {
        var (viewModel, _, _) = Create();

        Assert.Equal(
            ShellPageMap.Definitions.Select(d => d.Page),
            viewModel.NavItems.Select(n => n.Page));
        Assert.Equal(
            ShellPageMap.Definitions.Select(d => d.Title),
            viewModel.NavItems.Select(n => n.Title));
    }

    [Fact]
    public void SelectingNavItem_NavigatesImmediately()
    {
        var (viewModel, _, navigation) = Create();

        viewModel.SelectedNavItem = viewModel.NavItems.First(n => n.Page == ShellPage.Settings);

        // First entry is the startup navigation to the restored page (Vault by default).
        Assert.Equal([ShellPage.Vault, ShellPage.Settings], navigation.Navigations);
    }

    [Fact]
    public void SelectingNavItem_PersistsLastPage()
    {
        var (viewModel, settings, _) = Create();

        viewModel.SelectedNavItem = viewModel.NavItems.First(n => n.Page == ShellPage.Settings);

        Assert.Equal(ShellPage.Settings, settings.Current.LastPage);
    }

    [Fact]
    public void SelectingTheSameNavItemTwice_PersistsOnlyTheFirstChange()
    {
        var (viewModel, settings, navigation) = Create();
        var settingsItem = viewModel.NavItems.First(n => n.Page == ShellPage.Settings);

        viewModel.SelectedNavItem = settingsItem;
        var updatesAfterFirst = settings.Updates.Count;
        var navigationsAfterFirst = navigation.Navigations.Count;
        viewModel.SelectedNavItem = settingsItem;

        Assert.Equal(updatesAfterFirst, settings.Updates.Count);
        Assert.Equal(navigationsAfterFirst, navigation.Navigations.Count);
    }
}
