using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PaperRoute.Desktop.Tests;

// How a view is shown to people with no window opened: rendered through the
// headless Skia platform and saved as a PNG in a "pictures" folder inside the
// test results, which the CI "trial" job uploads with them (its artifact is the
// whole TestResults folder). The only assertion on a picture is that it is not
// blank; a person looks at it.
//
// Call it inside Headless.Run, on a window that has been shown.
internal static class Pictures
{
    // The variable the CI trial job sets to $GITHUB_WORKSPACE/TestResults/pictures,
    // inside the folder its artifact uploads.
    public const string DirectoryVariable = "PAPERROUTE_TRIAL_PICTURES";

    // Picture files go to the folder PAPERROUTE_TRIAL_PICTURES names, or else
    // to TestResults\pictures in the test output. MSTest does not tell a test
    // where "dotnet test --results-directory" points: under "dotnet test" every
    // TestContext directory is the output folder (checked 2026-11-19), so the
    // CI job names the folder itself.
    public static string Directory()
    {
        string? named = Environment.GetEnvironmentVariable(DirectoryVariable);
        return string.IsNullOrWhiteSpace(named)
            ? Path.Combine(AppContext.BaseDirectory, "TestResults", "pictures")
            : Path.GetFullPath(named);
    }

    // Lets transitions (100 ms hovers) finish, renders a frame, checks it is not
    // blank and saves it as <name>.png, or only the region given (a close-up,
    // in the window's pixels: headless draws at scale 1). Returns the file's
    // full path.
    public static string Save(Window window, string name, TestContext context, PixelRect? region = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Dispatcher.UIThread.RunJobs();
        // Each tick moves the animation clock one frame; 30 frames outlast every transition the styles declare.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(30);
        Dispatcher.UIThread.RunJobs();

        using WriteableBitmap? captured = window.CaptureRenderedFrame();
        Assert.IsNotNull(captured, $"The headless platform captured no frame for the picture \"{name}\".");
        using WriteableBitmap? cropped = region is { } part ? Crop(captured, part) : null;
        WriteableBitmap frame = cropped ?? captured;
        Assert.IsGreaterThan(1, CountColours(frame), $"The picture \"{name}\" is blank (a single colour).");

        string folder = Directory();
        System.IO.Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + ".png");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        context.WriteLine($"Picture saved: {path}");
        return path;
    }

    // The part of a frame inside a region (kept inside the frame).
    private static WriteableBitmap Crop(WriteableBitmap frame, PixelRect region)
    {
        using ILockedFramebuffer source = frame.Lock();
        PixelRect inside = region.Intersect(new PixelRect(source.Size));
        Assert.IsTrue(inside.Width > 0 && inside.Height > 0, $"The region {region} lies outside the frame {source.Size}.");
        var part = new WriteableBitmap(inside.Size, frame.Dpi, source.Format, AlphaFormat.Premul);
        using ILockedFramebuffer target = part.Lock();
        var row = new byte[inside.Width * 4];
        for (int y = 0; y < inside.Height; y++)
        {
            Marshal.Copy(source.Address + ((inside.Y + y) * source.RowBytes) + (inside.X * 4), row, 0, row.Length);
            Marshal.Copy(row, 0, target.Address + (y * target.RowBytes), row.Length);
        }

        return part;
    }

    // How many distinct colours the frame holds; 1 means nothing was drawn.
    public static int CountColours(WriteableBitmap frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        using ILockedFramebuffer buffer = frame.Lock();
        int width = buffer.Size.Width;
        int height = buffer.Size.Height;
        int stride = buffer.RowBytes;
        var bytes = new byte[stride * height];
        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

        var colours = new HashSet<uint>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                colours.Add(BitConverter.ToUInt32(bytes, (y * stride) + (x * 4)));
            }
        }

        return colours.Count;
    }
}
