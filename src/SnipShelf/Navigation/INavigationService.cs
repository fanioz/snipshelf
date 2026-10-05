using FluentAvalonia.UI.Controls;
using SnipShelf.Models;

namespace SnipShelf.Navigation;

/// <summary>
/// View-model-facing navigation over the FluentAvalonia Frame. Deliberately synchronous:
/// pane clicks must feel instant, and the only async part (persisting the last page) is
/// the settings service's job, not navigation's.
/// </summary>
public interface INavigationService
{
    /// <summary>Hands the navigation service its Frame. Called once, by the app, when the window exists.</summary>
    void Attach(Frame frame);

    void Navigate(ShellPage page);
}
