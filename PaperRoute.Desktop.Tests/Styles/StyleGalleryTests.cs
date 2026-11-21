using Avalonia.Controls;
using Avalonia.Styling;

namespace PaperRoute.Desktop.Tests.Styles;

// The style gallery saved as two pictures, Light and Dark, for the maintainer
// to look at (d1 8, step 6). The pictures land in TestResults\pictures, which
// the CI trial job uploads; the only assertion is that they are not blank.
[TestClass]
public sealed class StyleGalleryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TheStyleGalleryIsSavedInLight() => SaveGallery(ThemeVariant.Light, "style-gallery-light");

    [TestMethod]
    public void TheStyleGalleryIsSavedInDark() => SaveGallery(ThemeVariant.Dark, "style-gallery-dark");

    private void SaveGallery(ThemeVariant variant, string name)
    {
        string path = Headless.Run(() =>
        {
            var gallery = new StyleGallery();
            var window = new Window
            {
                Width = 1280,
                Height = 900,
                RequestedThemeVariant = variant,
                Content = gallery,
            };
            window.Bind(Window.BackgroundProperty, window.GetResourceObservable("BoardBackgroundBrush"));
            window.Show();
            try
            {
                gallery.FocusTheFocusedButton();
                return Pictures.Save(window, name, TestContext);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }
}
