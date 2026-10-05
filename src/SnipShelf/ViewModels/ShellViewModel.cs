using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using SnipShelf.Models;
using SnipShelf.Navigation;
using SnipShelf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SnipShelf.ViewModels;

/// <summary>One entry in the navigation pane.</summary>
public sealed class NavItemViewModel(ShellPageDefinition definition) : ViewModelBase
{
    public string Title { get; } = definition.Title;

    public ShellPage Page { get; } = definition.Page;

    // PathIconSource rather than a raw geometry: NavigationViewItem.IconSource wants an
    // IconSourceElement, and FluentAvalonia themes it (size, foreground) like any other icon.
    // Lazy because StreamGeometry needs the render platform; the view model must stay
    // constructible in plain unit tests, and the pane only materializes icons when it binds.
    private IconSource? _icon;

    public IconSource Icon => _icon ??= new PathIconSource
    {
        Data = StreamGeometry.Parse(definition.IconData),
    };
}

public partial class ShellViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly INavigationService _navigation;
    private bool _restoring;

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    [ObservableProperty]
    private NavItemViewModel? _selectedNavItem;

    public ShellViewModel(ISettingsService settings, INavigationService navigation)
    {
        _settings = settings;
        _navigation = navigation;

        NavItems = new ObservableCollection<NavItemViewModel>(
            ShellPageMap.Definitions.Select(d => new NavItemViewModel(d)));

        // Restore through the property so navigation fires (buffered — no Frame yet);
        // _restoring suppresses the disk write, since nothing actually changed.
        _restoring = true;
        var lastPage = settings.Load().LastPage;
        SelectedNavItem = NavItems.FirstOrDefault(n => n.Page == lastPage) ?? NavItems[0];
        _restoring = false;
    }

    partial void OnSelectedNavItemChanged(NavItemViewModel? oldValue, NavItemViewModel? newValue)
    {
        if (newValue is null)
        {
            return;
        }

        _navigation.Navigate(newValue.Page);

        if (!_restoring)
        {
            _ = PersistLastPageAsync(newValue.Page);
        }
    }

    // UpdateAsync throws on a failed write (disk full, permissions); the shell must observe
    // that instead of leaking an unobserved task. Navigation itself is unaffected.
    private async Task PersistLastPageAsync(ShellPage page)
    {
        try
        {
            await _settings.UpdateAsync(s => s.LastPage = page);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal end for the write.
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to persist last page: {ex.Message}");
        }
    }
}
