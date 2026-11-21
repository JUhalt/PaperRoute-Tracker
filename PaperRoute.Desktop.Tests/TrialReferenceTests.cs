using System.Reflection;

namespace PaperRoute.Desktop.Tests;

[TestClass]
public sealed class TrialReferenceTests
{
    // Rule P1 (d1 6.2): the trial's own assemblies reference nothing of the
    // Windows Forms app, its updater, Avalonia's paid developer tools or the
    // retired ItemsRepeater; the view models reference no Avalonia at all.
    private static readonly string[] s_forbidden =
    [
        "System.Windows.Forms",
        "System.Drawing.Common",
        "PaperRouteTracker",
        "Velopack",
        "AvaloniaUI.DiagnosticsSupport",
        "Avalonia.Controls.ItemsRepeater",
    ];

    [TestMethod]
    public void TrialAssembliesReferenceNoneOfTheForbiddenAssemblies()
    {
        Assembly desktop = typeof(App).Assembly;
        Assembly viewModels = Assembly.Load("PaperRoute.ViewModels");

        foreach (Assembly assembly in new[] { desktop, viewModels })
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                Assert.IsFalse(
                    s_forbidden.Contains(reference.Name, StringComparer.OrdinalIgnoreCase),
                    $"{assembly.GetName().Name} references {reference.Name}.");
            }
        }

        foreach (AssemblyName reference in viewModels.GetReferencedAssemblies())
        {
            Assert.IsFalse(
                reference.Name?.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ?? false,
                $"PaperRoute.ViewModels references {reference.Name}; the view models must not use Avalonia.");
        }
    }
}
