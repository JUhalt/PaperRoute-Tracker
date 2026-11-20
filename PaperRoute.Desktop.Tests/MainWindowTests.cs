using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace PaperRoute.Desktop.Tests;

[TestClass]
public sealed class MainWindowTests
{
    // The trial's first view test (d1 8, step 2): the main window opens under the
    // headless platform and draws through Skia. The window is still empty, so the
    // test gives it one line of text; a frame of a single colour means nothing was drawn.
    [TestMethod]
    public void MainWindowRendersAFrameThatIsNotBlank()
    {
        (int width, int height, int colours) = Headless.Run(() =>
        {
            var window = new MainWindow
            {
                Width = 320,
                Height = 160,
                Content = new TextBlock { Text = "PaperRoute", FontSize = 32 },
            };
            window.Show();
            try
            {
                using WriteableBitmap? frame = window.CaptureRenderedFrame();
                Assert.IsNotNull(frame, "The headless platform captured no frame.");
                return CountColours(frame);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsGreaterThan(0, width, "The captured frame has no width.");
        Assert.IsGreaterThan(0, height, "The captured frame has no height.");
        Assert.IsGreaterThan(1, colours, "The captured frame is blank (a single colour).");
    }

    private static (int Width, int Height, int Colours) CountColours(WriteableBitmap frame)
    {
        using var buffer = frame.Lock();
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

        return (width, height, colours.Count);
    }
}
