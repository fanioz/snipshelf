using Avalonia.Media;
using SnipShelf.Models;
using SnipShelf.ViewModels;
using SnipShelf.Views;

namespace SnipShelf.Navigation;

/// <summary>One destination in the navigation pane.</summary>
public sealed record ShellPageDefinition(
    ShellPage Page,
    string Title,
    string IconData,
    Type ViewModelType,
    Type ViewType);

/// <summary>
/// Single source of truth mapping <see cref="ShellPage"/> to its view model and view.
/// The DI container, the navigation pane, and the Frame page factory all read from here,
/// so a page cannot exist in one and be missing in another.
/// </summary>
public static class ShellPageMap
{
    // Order here is the order in the navigation pane.
    public static IReadOnlyList<ShellPageDefinition> Definitions { get; } =
    [
        new(
            ShellPage.Vault,
            "Vault",
            "M2,4 L14,4 L14,13 L2,13 Z M2,4 L4,2 L12,2 L14,4 M5,7 L11,7 M5,10 L11,10",
            typeof(VaultViewModel),
            typeof(VaultView)),
        new(
            ShellPage.Favorites,
            "Favorites",
            "M8,1.8 L9.9,5.7 L14.2,6.3 L11.1,9.3 L11.8,13.6 L8,11.6 L4.2,13.6 L4.9,9.3 L1.8,6.3 L6.1,5.7 Z",
            typeof(FavoritesViewModel),
            typeof(FavoritesView)),
        new(
            ShellPage.Settings,
            "Settings",
            "M2,4.5 L7,4.5 M9,4.5 L14,4.5 M2,11.5 L6,11.5 M10,11.5 L14,11.5 "
                + "M5.5,3 A1.5,1.5 0,1,0 5.501,3 Z M11.5,10 A1.5,1.5 0,1,0 11.501,10 Z",
            typeof(SettingsViewModel),
            typeof(SettingsView)),
    ];

    public static ShellPageDefinition For(ShellPage page) =>
        Definitions.FirstOrDefault(d => d.Page == page)
            ?? throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown shell page.");

    public static bool TryGetByViewModelType(Type viewModelType, out ShellPageDefinition definition)
    {
        foreach (var candidate in Definitions)
        {
            if (candidate.ViewModelType == viewModelType)
            {
                definition = candidate;
                return true;
            }
        }

        definition = null!;
        return false;
    }
}
