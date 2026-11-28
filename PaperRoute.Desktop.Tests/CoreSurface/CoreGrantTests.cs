using System.Reflection;
using System.Runtime.CompilerServices;
using ManuscriptPipeline.Models;

namespace PaperRoute.Desktop.Tests.CoreSurface;

// The Friend rule (start-up design 2.1): every use of a Friend core member
// lives in PaperRoute.ViewModels or this test project, which the core grants
// its internals to, and never in PaperRoute.Desktop. The compiler already
// refuses a Friend use in PaperRoute.Desktop while the core grants it
// nothing; this test keeps the grant from being added by accident, which
// would lift the rule without a word.
[TestClass]
public sealed class CoreGrantTests
{
    [TestMethod]
    public void TheCoreGrantsNoInternalsToTheDesktopProject()
    {
        Assembly core = typeof(Manuscript).Assembly;
        string[] grants = [.. core.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(grant => grant.AssemblyName.Split(',')[0].Trim())
            .Order(StringComparer.Ordinal)];

        // The reader is live: the core's grant to the view models is read.
        CollectionAssert.Contains(grants, "PaperRoute.ViewModels",
            $"The core's InternalsVisibleTo list was not read: {string.Join(", ", grants)}.");

        string desktop = typeof(App).Assembly.GetName().Name
            ?? throw new InvalidOperationException("PaperRoute.Desktop has no name.");
        string[] toDesktop = [.. grants.Where(grant => string.Equals(grant, desktop, StringComparison.OrdinalIgnoreCase))];
        Assert.IsEmpty(toDesktop,
            $"PaperRoute.Core grants its internals to {desktop} (PaperRoute.Core/Properties/AssemblyInfo.vb). Every use of a " +
            "Friend core member belongs in PaperRoute.ViewModels, where FriendUseTests lists it (start-up design 2.1, the " +
            "Friend rule): remove the grant.");
    }
}
