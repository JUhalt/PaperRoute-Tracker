using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Notices;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Shell;

// The notice strip's model (shell design 2.4, pull request B3): the status
// line each result replaces, and the standing notices that stay until
// withdrawn, the same key replacing, Failure before Warning before Info and
// the newest first within a kind. No window: the strip's view comes later.
[TestClass]
public sealed class NoticeStripTests
{
    private static readonly ICommand s_nothing = new RelayCommand(() => { });

    [TestMethod]
    public void ANoticeReplacesTheStatusLine()
    {
        var strip = new NoticeStripViewModel();
        var changes = new List<string?>();
        strip.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        strip.Notice("Loaded 7 manuscripts from local storage.");
        strip.Notice("Board sorted by title.");

        Assert.AreEqual("Board sorted by title.", strip.Status, "A result replaces the status line; it is not added to it.");
        Assert.HasCount(2, changes.Where(name => name == nameof(NoticeStripViewModel.Status)).ToList(),
            "Each result raises the change the live status line is read from.");
        Assert.IsEmpty(strip.StandingNotices, "A result is not a notice that stays.");
    }

    [TestMethod]
    public void AStandingNoticeStaysUntilWithdrawn()
    {
        var strip = new NoticeStripViewModel();
        StandingNotice reminder = Standing("startup-reminder", "PaperRoute does not start with Linux.", NoticeKind.Info);
        strip.Notice("Loaded 7 manuscripts from local storage.");
        var statusChanges = 0;
        strip.PropertyChanged += (_, e) => statusChanges += e.PropertyName == nameof(NoticeStripViewModel.Status) ? 1 : 0;

        strip.Show(reminder);
        strip.Withdraw("Startup-Reminder");

        Assert.AreEqual("Loaded 7 manuscripts from local storage.", strip.Status,
            "A standing notice never touches the status line: the last result stays.");
        Assert.AreEqual(0, statusChanges, "Showing or withdrawing a standing notice does not re-announce the last result.");
        CollectionAssert.AreEqual(new[] { reminder }, strip.StandingNotices.ToArray(),
            "Keys are compared exactly: a key in other letter case withdraws nothing.");

        strip.Notice("Board sorted by title.");
        strip.Withdraw("update-available");

        CollectionAssert.AreEqual(new[] { reminder }, strip.StandingNotices.ToArray(),
            "Results and the withdrawal of another key leave a standing notice where it is.");

        strip.Withdraw("startup-reminder");

        Assert.IsEmpty(strip.StandingNotices, "A withdrawn notice is gone.");
        Assert.AreEqual("Board sorted by title.", strip.Status, "Withdrawing a standing notice leaves the status line.");
        Assert.AreEqual(1, statusChanges, "Only the result raised the status line's change; the withdrawal did not.");
        strip.Withdraw("startup-reminder");
        Assert.IsEmpty(strip.StandingNotices, "Withdrawing a key no longer shown changes nothing.");
    }

    [TestMethod]
    public void TheSameKeyReplacesNotStacks()
    {
        // The old notice sits below a newer one of its kind, so its old place
        // and the new one's place differ: a replacement in the old place fails.
        var strip = new NoticeStripViewModel();
        StandingNotice network = Standing("network", "PaperRoute cannot reach the network.", NoticeKind.Warning);
        StandingNotice update = Standing("update-available", "PaperRoute 1.1.1 is available.", NoticeKind.Info);
        strip.Show(Standing("storage", "The library folder is read-only.", NoticeKind.Warning));
        strip.Show(network);
        strip.Show(update);
        StandingNotice worse = Standing("storage", "The library folder cannot be reached.", NoticeKind.Failure);

        strip.Show(worse);

        CollectionAssert.AreEqual(new[] { worse, network, update }, strip.StandingNotices.ToArray(),
            "A notice shown again under its key replaces the old one and goes where its kind ranks it, a Failure above the Warnings; it does not stack.");

        // Of the same kind: the notice shown again is the newest of its kind.
        StandingNotice folder = Standing("folder", "The attachments folder is read-only.", NoticeKind.Warning);
        StandingNotice networkAgain = Standing("network", "PaperRoute still cannot reach the network.", NoticeKind.Warning);
        strip.Show(folder);

        strip.Show(networkAgain);

        CollectionAssert.AreEqual(new[] { worse, networkAgain, folder, update }, strip.StandingNotices.ToArray(),
            "A notice shown again under its key goes above the others of its kind, as the newest of them.");
    }

    [TestMethod]
    public void AFailureShowsBeforeAWarning()
    {
        var strip = new NoticeStripViewModel();
        StandingNotice olderInfo = Standing("older-info", "Older info.", NoticeKind.Info);
        StandingNotice olderWarning = Standing("older-warning", "Older warning.", NoticeKind.Warning);
        StandingNotice failure = Standing("failure", "A failure.", NoticeKind.Failure);
        StandingNotice newerWarning = Standing("newer-warning", "Newer warning.", NoticeKind.Warning);
        StandingNotice newerInfo = Standing("newer-info", "Newer info.", NoticeKind.Info);

        foreach (StandingNotice notice in new[] { olderInfo, olderWarning, failure, newerWarning, newerInfo })
        {
            strip.Show(notice);
        }

        CollectionAssert.AreEqual(
            new[] { failure, newerWarning, olderWarning, newerInfo, olderInfo },
            strip.StandingNotices.ToArray(),
            "Failure first, then Warning, then Info, the newest first within a kind, whatever order they came in.");
    }

    // Through the shell, as a page gives them: today the failed-save warning
    // hides every other text (Form1.vb:655-661); here a result still shows,
    // and the shell's notice is the strip's status line.
    [TestMethod]
    public void ResultsStillShowBesideAStandingNotice()
    {
        string root = Path.Combine(Path.GetTempPath(), "PaperRoute-NoticeStrip-" + Guid.NewGuid().ToString("N"));
        using var session = new DiskLibrarySession(
            Path.Combine(root, "data"), Path.Combine(root, "library"), root, LibraryKind.Example, LargeLibraryFixture.Today);
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        IInteractions interactions = shell;
        var changes = new List<string?>();
        ((INotifyPropertyChanged)shell).PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        StandingNotice saveFailed = Standing("save-failed", "Warning: latest changes were not saved.", NoticeKind.Failure);

        interactions.Show(saveFailed);
        interactions.Notice("Board sorted by title.");

        Assert.AreEqual("Board sorted by title.", shell.Notice, "A result shows while a failure stands.");
        Assert.AreEqual(shell.NoticeStrip.Status, shell.Notice, "The shell's notice is the strip's status line.");
        Assert.Contains(nameof(ShellViewModel.Notice), changes, "The shell raises its notice's change for the window's binding.");
        CollectionAssert.AreEqual(new[] { saveFailed }, shell.NoticeStrip.StandingNotices.ToArray(),
            "The standing notice stays beside the result.");

        interactions.Withdraw("save-failed");

        Assert.IsEmpty(shell.NoticeStrip.StandingNotices, "The shell withdraws through the strip.");
        Assert.AreEqual("Board sorted by title.", shell.Notice, "Withdrawing a standing notice leaves the status line.");
        Assert.IsFalse(Directory.Exists(root), "Nothing here touches the disk.");
    }

    [TestMethod]
    public void NullTextBecomesEmpty()
    {
        var strip = new NoticeStripViewModel();
        strip.Notice("Loaded 7 manuscripts from local storage.");

        strip.Notice(null);

        Assert.AreEqual(string.Empty, strip.Status, "A null result empties the status line, as the trial's shell did.");
    }

    // Not in the design's list of six: the records refuse what would leave a
    // notice without a key to withdraw it by, a notice or button without a name
    // for a screen reader, or a button that does nothing.
    [TestMethod]
    public void AStandingNoticeNeedsAKeyAndAText()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new StandingNotice(" ", "Some text.", NoticeKind.Info, []),
            "A blank key is refused.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new StandingNotice(null!, "Some text.", NoticeKind.Info, []),
            "A missing key is refused.");
        Assert.ThrowsExactly<ArgumentException>(() => new StandingNotice("key", " ", NoticeKind.Info, []),
            "A blank text is refused: it would give the notice an empty accessible name.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new StandingNotice("key", null!, NoticeKind.Info, []),
            "A missing text is refused.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new StandingNotice("key", "Some text.", NoticeKind.Info, null!),
            "Missing actions are refused; a notice without buttons has an empty list.");
        Assert.ThrowsExactly<ArgumentException>(() => new NoticeAction(" ", s_nothing),
            "A blank button label is refused: it would give the button an empty accessible name.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new NoticeAction("Dismiss", null!),
            "A button without a command is refused.");

        StandingNotice plain = new("key", "Some text.", NoticeKind.Info, []);
        Assert.IsEmpty(plain.Actions, "A notice without buttons is allowed.");
    }

    private static StandingNotice Standing(string key, string text, NoticeKind kind) =>
        new(key, text, kind, [new NoticeAction("Dismiss", s_nothing)]);
}
