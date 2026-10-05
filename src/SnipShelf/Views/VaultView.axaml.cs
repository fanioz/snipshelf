using Avalonia.Controls;
using Avalonia.Interactivity;
using SnipShelf.ViewModels;
using System.Threading.Tasks;

namespace SnipShelf.Views;

public partial class VaultView : UserControl
{
    public VaultView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    /// <summary>
    /// Cancels pending debounced searches when the vault view leaves the visual tree.
    /// The in-flight task's own finally block disposes its cancellation source, so
    /// Dispose must only cancel.
    /// </summary>
    /// <param name="sender">The control that raised the unloaded event.</param>
    /// <param name="e">The routed event arguments.</param>
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }
}
