using Avalonia;
using Avalonia.Headless;

namespace PaperRoute.Desktop.Tests;

// The type handed to HeadlessUnitTestSession.StartNew (d1 6.1). Not App itself:
// StartNew uses this static BuildAvaloniaApp, so the views draw through Skia
// with the Inter font, as the real window does, and text is measured for real.
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}
