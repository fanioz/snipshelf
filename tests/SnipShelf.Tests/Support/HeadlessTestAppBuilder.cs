using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(SnipShelf.Tests.Support.HeadlessTestAppBuilder))]

namespace SnipShelf.Tests.Support;

public sealed class HeadlessApp : Application
{
    public override void Initialize()
    {
    }
}

public static class HeadlessTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
