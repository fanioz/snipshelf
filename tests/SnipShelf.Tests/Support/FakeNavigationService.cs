using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using SnipShelf.Models;
using SnipShelf.Navigation;

namespace SnipShelf.Tests.Support;

/// <summary>Records navigation calls instead of touching a Frame.</summary>
public sealed class FakeNavigationService : INavigationService
{
    public List<ShellPage> Navigations { get; } = [];

    public List<Frame> Attachments { get; } = [];

    public void Attach(Frame frame) => Attachments.Add(frame);

    public void Navigate(ShellPage page) => Navigations.Add(page);
}
