using System.Reflection;

namespace PaperRoute.Desktop.Tests;

[TestClass]
public sealed class TrialReferenceTests
{
    // Rule P1 (d1 6.2), split in two lists by the start-up design (build 6,
    // G1): the trial's own assemblies reference nothing of the Windows Forms
    // app, Avalonia's paid developer tools or the retired ItemsRepeater.
    // Velopack, the updater, is allowed in PaperRoute.Desktop alone, whose
    // Main runs it first (MainCallsVelopackFirst); the view models reference
    // neither it nor any Avalonia.
    private static readonly string[] s_forbiddenInBoth =
    [
        "System.Windows.Forms",
        "System.Drawing.Common",
        "PaperRouteTracker",
        "AvaloniaUI.DiagnosticsSupport",
        "Avalonia.Controls.ItemsRepeater",
    ];

    private static readonly string[] s_forbiddenInViewModels = [.. s_forbiddenInBoth, "Velopack"];

    [TestMethod]
    public void TrialAssembliesReferenceNoneOfTheForbiddenAssemblies()
    {
        Assembly desktop = typeof(App).Assembly;
        Assembly viewModels = Assembly.Load("PaperRoute.ViewModels");

        foreach ((Assembly assembly, string[] forbidden) in new[] { (desktop, s_forbiddenInBoth), (viewModels, s_forbiddenInViewModels) })
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                Assert.IsFalse(
                    forbidden.Contains(reference.Name, StringComparer.OrdinalIgnoreCase),
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
