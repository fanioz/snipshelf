using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using SnipShelf.Models;
using SnipShelf.Navigation;
using SnipShelf.Services;
using SnipShelf.Tests.Support;
using SnipShelf.ViewModels;
using SnipShelf.Views;

namespace SnipShelf.Tests.Navigation;

public sealed class FrameNavigationServiceTests
{
    private static FrameNavigationService CreateService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(new FakeSettingsService());
        foreach (var definition in ShellPageMap.Definitions)
        {
            services.AddTransient(definition.ViewModelType);
        }

        return new FrameNavigationService(services.BuildServiceProvider());
    }

    [AvaloniaFact]
    public void Attach_EnablesFactoryAndDisablesPageCache()
    {
        var navigation = CreateService();
        var frame = new Frame();

        navigation.Attach(frame);

        Assert.Same(navigation, frame.NavigationPageFactory);
        Assert.Equal(0, frame.CacheSize);
    }

    [AvaloniaFact]
    public void Navigate_BeforeAttach_BuffersUntilFrameExists()
    {
        var navigation = CreateService();
        navigation.Navigate(ShellPage.Favorites);

        var frame = new Frame();
        navigation.Attach(frame);

        Assert.IsType<FavoritesView>(frame.Content);
        Assert.IsType<FavoritesViewModel>(((FavoritesView)frame.Content).DataContext);
    }

    [AvaloniaFact]
    public void Navigate_ShowsThePagesViewWithAFreshViewModel()
    {
        var navigation = CreateService();
        var frame = new Frame();
        navigation.Attach(frame);

        navigation.Navigate(ShellPage.Vault);

        var page = Assert.IsType<VaultView>(frame.Content);
        Assert.IsType<VaultViewModel>(page.DataContext);
    }

    [AvaloniaFact]
    public void Navigate_Twice_ReplacesContentAndKeepsNoHistory()
    {
        var navigation = CreateService();
        var frame = new Frame();
        navigation.Attach(frame);

        navigation.Navigate(ShellPage.Vault);
        navigation.Navigate(ShellPage.Settings);

        Assert.IsType<SettingsView>(frame.Content);
        Assert.Equal(0, frame.BackStackDepth);
    }

    [Fact]
    public void GetPage_EveryShellPage_ReturnsItsViewWiredToItsViewModel()
    {
        var navigation = CreateService();

        foreach (var definition in ShellPageMap.Definitions)
        {
            var page = navigation.GetPage(definition.ViewModelType);

            Assert.NotNull(page);
            Assert.IsType(definition.ViewType, page);
            Assert.IsType(definition.ViewModelType, page!.DataContext);
        }
    }

    [Fact]
    public void GetPage_TwiceForSamePage_CreatesDistinctInstances()
    {
        var navigation = CreateService();

        var first = navigation.GetPage(typeof(VaultViewModel));
        var second = navigation.GetPage(typeof(VaultViewModel));

        Assert.NotSame(first, second);
        Assert.NotSame(first!.DataContext, second!.DataContext);
    }

    [Fact]
    public void GetPage_UnknownType_ReturnsNull()
    {
        var navigation = CreateService();

        Assert.Null(navigation.GetPage(typeof(string)));
    }

    [Fact]
    public void GetPageFromObject_WrapsTheGivenViewModel()
    {
        var navigation = CreateService();
        var viewModel = new SettingsViewModel(new FakeSettingsService());

        var page = navigation.GetPageFromObject(viewModel);

        Assert.IsType<SettingsView>(page);
        Assert.Same(viewModel, page!.DataContext);
    }

    [Fact]
    public void GetPageFromObject_NonShellViewModel_ReturnsNull()
    {
        var navigation = CreateService();

        Assert.Null(navigation.GetPageFromObject(new object()));
    }

    [Fact]
    public void Navigate_UnknownPage_Throws()
    {
        var navigation = CreateService();

        Assert.Throws<ArgumentOutOfRangeException>(() => navigation.Navigate((ShellPage)99));
    }
}
