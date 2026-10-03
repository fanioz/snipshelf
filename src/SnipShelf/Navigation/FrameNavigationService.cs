using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using SnipShelf.Models;
using SnipShelf.ViewModels;
using System;

namespace SnipShelf.Navigation;

/// <summary>
/// Drives a FluentAvalonia <see cref="Frame"/> from <see cref="ShellPageMap"/>. Page
/// view models come from the DI container; each navigation builds a fresh view so pages
/// never carry state across visits. Shell panes are destinations, not history: the Frame's
/// navigation stack and page cache are off.
/// </summary>
public sealed class FrameNavigationService : INavigationService, INavigationPageFactory
{
    private readonly IServiceProvider _services;
    private Frame? _frame;
    private ShellPage? _pending;

    public FrameNavigationService(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>Called once by the app when the Frame exists. Flushes any navigation that arrived first.</summary>
    public void Attach(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        frame.NavigationPageFactory = this;
        frame.CacheSize = 0;

        if (_pending is { } page)
        {
            _pending = null;
            Navigate(page);
        }
    }

    public void Navigate(ShellPage page)
    {
        var definition = ShellPageMap.For(page);

        // The shell constructs its view model before the window exists; remember the
        // request and replay it on Attach.
        if (_frame is null)
        {
            _pending = page;
            return;
        }

        _frame.NavigateToType(
            definition.ViewModelType,
            parameter: null,
            new FrameNavigationOptions { IsNavigationStackEnabled = false });
    }

    public Control? GetPage(Type pageType)
    {
        if (!ShellPageMap.TryGetByViewModelType(pageType, out var definition))
        {
            return null;
        }

        return CreateView(definition.ViewModelType, definition.ViewType);
    }

    public Control? GetPageFromObject(object obj)
    {
        if (obj is not ViewModelBase viewModel ||
            !ShellPageMap.TryGetByViewModelType(viewModel.GetType(), out var definition))
        {
            return null;
        }

        var view = CreateView(definition.ViewModelType, definition.ViewType);
        view.DataContext = viewModel;
        return view;
    }

    private Control CreateView(Type viewModelType, Type viewType)
    {
        var viewModel = _services.GetRequiredService(viewModelType);
        var view = (Control)Activator.CreateInstance(viewType)!;
        view.DataContext = viewModel;
        return view;
    }
}
