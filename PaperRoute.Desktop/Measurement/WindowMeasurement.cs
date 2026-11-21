using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Measurement;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Measurement;

// "--measure <file>.json" (d1 7.3): the rows of d1 6.3 timed on a real window
// at 1280 x 800, the library the session holds (the example, or with
// "--snapshot-from" a read-only copy of a library folder), written as JSON,
// and the window closed. It runs once the app's dispatcher has started; a
// frame is the next two animation frames, so the one asked for has been drawn.
//
// The process holds one session (d1 6.1), so the example cannot run first to
// warm the process as it does in the headless test: the startup row here
// includes the Board's own first-time costs, and a row from the process start
// is reported beside it.
public static class WindowMeasurement
{
    public const string Mode = "window";
    public const double Width = 1280;
    public const double Height = 800;

    private static readonly TimeSpan s_frameLimit = TimeSpan.FromSeconds(5);

    // Measures, writes the JSON at path (accepted by BoardMeasurementReport.RefusalFor)
    // and returns the exit code: 0 when every row was measured, else 1 (the
    // JSON then holds the rows measured so far and the error).
    public static int Run(ILibrarySession session, string path, string daySource)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var measurement = new BoardMeasurement(WaitForFrame);
        string library = session.Kind == LibraryKind.Snapshot ? "snapshot" : "example";
        MainWindow? window = null;
        ShellViewModel? shell = null;
        string? error = null;
        double dpi = 0;
        try
        {
            (window, shell) = measurement.Start(library, session, variant: null, Width, Height);
            dpi = window.RenderScaling * 96;
            double sinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            measurement.Add(new MeasuredRow(library, BoardMeasurement.ProcessStartToBoard,
                "From the process start to an interactive Board (.NET and Avalonia start-up included, cold)",
                sinceStart, sinceStart, BoardMeasurement.StartupBudget, "warm startup", Informational: true));
            measurement.MeasureBoard(library, window, shell, session, requireChanges: session.Kind == LibraryKind.Snapshot);
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
        }

        var report = new BoardMeasurementReport
        {
            Mode = Mode,
            MachineName = Environment.MachineName,
            Dpi = dpi,
            Option = BoardMeasurement.Option,
            ResetThreshold = shell?.Board.ResetThreshold ?? SyncedCollection<CardViewModel>.DefaultResetThreshold,
            Day = session.Today,
            DaySource = daySource,
            Build = BoardMeasurement.Build,
            Multiplier = BoardMeasurement.BudgetMultiplier(),
            TakenAt = DateTime.Now,
            WaitingForRowsOfN = BoardMeasurement.WaitingForRowsOfN,
            HeldLibrary = session.Kind == LibraryKind.Snapshot ? library : string.Empty,
            Error = error,
            Rows = measurement.Rows,
        };

        try
        {
            report.WriteJson(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"The measurement could not be written to {path}: {ex.Message}");
            error ??= ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"The measurement could not be written to {path}: {ex.Message}");
            error ??= ex.Message;
        }

        window?.Close();
        return error is null ? 0 : 1;
    }

    // Waits for the window's next two animation frames: the first is the one
    // the change asked for, drawn once the second begins. A window that draws
    // nothing for 5 s (minimized, or the screen locked) stops the run.
    public static void WaitForFrame(Window window) => WaitForFrames(window, 2, s_frameLimit);

    public static void WaitForFrames(Window window, int count, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(window);
        var frame = new DispatcherFrame();
        int seen = 0;
        bool timedOut = false;

        void Next(TimeSpan _)
        {
            seen++;
            if (seen >= count)
            {
                frame.Continue = false;
            }
            else
            {
                window.RequestAnimationFrame(Next);
            }
        }

        window.RequestAnimationFrame(Next);
        var stop = new DispatcherTimer(limit, DispatcherPriority.Background, Dispatcher.UIThread, (_, _) =>
        {
            timedOut = true;
            frame.Continue = false;
        });
        stop.Start();
        try
        {
            Dispatcher.UIThread.PushFrame(frame);
        }
        finally
        {
            stop.Stop();
        }

        if (timedOut && seen < count)
        {
            throw new TimeoutException($"The window drew {seen} of {count} frames within {limit.TotalSeconds} s: is it minimized, or the screen locked?");
        }
    }
}
