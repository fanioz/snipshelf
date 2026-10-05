using Avalonia;
using System;

namespace SnipShelf;

sealed class Program
{
    // Avalonia configuration, usage tracking, and startup exception handling can be found in
    // https://github.com/AvaloniaUI/Avalonia/wiki/Platform-specific-features

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
