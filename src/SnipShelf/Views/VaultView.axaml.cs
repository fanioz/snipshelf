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
}
