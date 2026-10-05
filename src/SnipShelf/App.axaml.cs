using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using SnipShelf.Models;
using SnipShelf.Navigation;
using SnipShelf.Services;
using SnipShelf.Theming;
using SnipShelf.ViewModels;
using SnipShelf.Views;
using System;
using System.IO;

namespace SnipShelf;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = BuildServices();

        // Theme must be decided before the first frame is drawn, so this is synchronous.
        var settings = services.GetRequiredService<ISettingsService>();
        ApplyTheme(settings.Load().Theme);
        settings.Changed += (_, current) =>
            Dispatcher.UIThread.Post(() => ApplyTheme(current.Theme));

        // The Vault is empty until the schema exists; blocking here keeps the first Vault
        // render from racing the data layer. It is milliseconds on a cold start and runs
        // before any window is shown.
        try
        {
            services.GetRequiredService<DatabaseBootstrapper>().InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Nothing renders without the vault and no window exists yet to host an error
            // view, so surface the failure itself instead of crashing before anything shows.
            new Window
            {
                Title = "SnipShelf",
                Width = 420,
                Height = 200,
                Content = new TextBlock
                {
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap,
                    Text = $"SnipShelf could not open its vault:\n{ex.Message}",
                },
            }.Show();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = services.GetRequiredService<ShellViewModel>(),
            };

            // Composition only: the shell builds its view model before any window exists,
            // so the Frame is handed to the navigation service here and pending
            // navigation replays on attach.
            services.GetRequiredService<INavigationService>()
                .Attach(window.FindControl<Frame>("PageFrame")!);

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SnipShelf");
        services.AddSingleton<ISettingsService>(new SettingsService(dataFolder));

        services.AddSingleton(new VaultDatabase(dataFolder));
        services.AddSingleton<DatabaseBootstrapper>();
        services.AddSingleton<ISnippetRepository>(sp => new SqliteSnippetRepository(
            sp.GetRequiredService<VaultDatabase>(),
            TimeProvider.System));

        // Page view models are transient: the Frame rebuilds a page on every visit, and a
        // page that outlived its visit would resurrect stale scroll or selection state.
        foreach (var definition in ShellPageMap.Definitions)
        {
            services.AddTransient(definition.ViewModelType);
        }

        services.AddSingleton<FrameNavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<FrameNavigationService>());
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }

    private void ApplyTheme(AppTheme theme) =>
        RequestedThemeVariant = theme.ToThemeVariant();
}
