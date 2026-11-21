using Avalonia.Controls;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The Avalonia test project's own catalog of target surfaces (the migration
// plan's section 3, check 1; d1 6.2 V22): every surface the new interface has,
// each with a factory that builds it over a library, so a test can show each
// one. The trial starts it with one entry, the Board. Mapping today's Windows
// Forms catalog keys (85 of them) onto it is left to unit 1.
internal static class TargetSurfaceCatalog
{
    // The libraries a factory can be given.
    public const string ExampleLibrary = "example";

    public static IReadOnlyList<TargetSurface> Surfaces { get; } =
    [
        new TargetSurface(
            "Board",
            ExampleLibrary,
            typeof(BoardView),
            session =>
            {
                ShellViewModel shell = App.CreateShell(session);
                shell.Board.Render();
                return new BoardView { DataContext = shell.Board };
            }),
    ];
}

// One surface: its key, the library its factory wants, the type of control the
// factory returns, and the factory. Call the factory on the interface thread,
// over a library already loaded.
internal sealed record TargetSurface(string Key, string Library, Type ViewType, Func<ILibrarySession, Control> Create);
