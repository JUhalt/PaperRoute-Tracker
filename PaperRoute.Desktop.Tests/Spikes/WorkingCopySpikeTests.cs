using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.Desktop.Tests.Input;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Spikes;

namespace PaperRoute.Desktop.Tests.Spikes;

// The working-copy spike (d1 7.2 item 4; plan section 10): today's record
// state (original, working clone, baseline snapshot) under a bound view model,
// typed into through a real TextBox with a compiled binding in a headless
// window. The manuscript is the example library's, built in memory; nothing is
// read from or written to disk, under the assembly's storage guard.
[TestClass]
public sealed class WorkingCopySpikeTests : Board.StorageCheckedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TypingChangesTheWorkingCloneAndNotTheOriginal()
    {
        Manuscript original = ExampleManuscript();
        string titleBefore = original.Title;
        string originalBefore = JsonSerializer.Serialize(original);
        var editor = new WorkingCopyEditorViewModel(RecordState.Load(original));

        (string boxText, string workingTitle) = Headless.Run(() =>
        {
            using var window = Open(editor);
            window.Type(" (second draft)");
            return (window.Box.Text ?? string.Empty, editor.Record.Working.Title);
        });

        Assert.AreEqual(titleBefore + " (second draft)", boxText, "The box shows what was typed.");
        Assert.AreEqual(boxText, workingTitle, "The binding wrote the typed text into the working clone.");
        Assert.AreEqual(boxText, editor.Title);
        Assert.AreNotSame(original, editor.Record.Working, "The working copy is a clone, not the record.");
        Assert.AreEqual(titleBefore, original.Title, "The original's title is untouched.");
        Assert.AreEqual(originalBefore, JsonSerializer.Serialize(original), "Nothing of the original changed.");
    }

    [TestMethod]
    public void UnsavedChangesGoTrueOnAnEditAndFalseWhenTheOldValueIsTypedBack()
    {
        Manuscript original = ExampleManuscript();
        string oldTitle = original.Title;
        var editor = new WorkingCopyEditorViewModel(RecordState.Load(original));
        Assert.IsFalse(editor.HasUnsavedChanges, "A record just loaded has no unsaved changes.");

        List<string> steps = Headless.Run(() =>
        {
            using var window = Open(editor);
            var steps = new List<string>();
            int snapshots = editor.Record.SnapshotCount;
            void Record(string step)
            {
                steps.Add($"{step}: unsaved {editor.HasUnsavedChanges}, line shown {window.UnsavedLine.IsVisible}, snapshots {editor.Record.SnapshotCount - snapshots}");
                snapshots = editor.Record.SnapshotCount;
            }

            Record("shown");
            window.Type("X");
            Record("typed X");
            window.Press(Key.Back);
            Record("Backspace");
            window.Box.SelectAll();
            window.Type("Another title");
            Record("select all, typed a new title (13 characters)");
            window.Box.SelectAll();
            window.Type(oldTitle);
            Record($"select all, typed the old title back ({oldTitle.Length} characters)");
            return steps;
        });

        foreach (string step in steps)
        {
            TestContext.WriteLine(step);
        }

        Assert.HasCount(5, steps);
        Assert.StartsWith("shown: unsaved False, line shown False", steps[0]);
        Assert.StartsWith("typed X: unsaved True, line shown True", steps[1]);
        Assert.StartsWith("Backspace: unsaved False, line shown False", steps[2]);
        Assert.StartsWith("select all, typed a new title (13 characters): unsaved True, line shown True", steps[3]);
        Assert.Contains(": unsaved False, line shown False", steps[4]);
        Assert.AreEqual(oldTitle, editor.Record.Working.Title);

        // The cost the plan asks about: one snapshot (a full serialization of the
        // working clone) per change of the box's text, timed here on the example's
        // largest record and on the largest of a 60-record synthetic library.
        TimeSnapshots("example", ExampleLibraryService.Create(LargeLibraryFixture.Today).Manuscripts);
        string root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialWorkingCopy-" + Guid.NewGuid().ToString("N"));
        try
        {
            TimeSnapshots("synthetic", LargeLibraryFixture.Create(root, 60).Manuscripts);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Left for Windows' temporary-file clean-up.
            }
        }
    }

    [TestMethod]
    public void NothingIsSaved()
    {
        string[] words = ["Save", "Commit", "Apply", "Backup", "Restore", "Recover", "Write", "Delete", "Persist", "Store"];
        foreach (Type type in new[] { typeof(RecordState), typeof(WorkingCopyEditorViewModel) })
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.IsFalse(
                    words.Any(word => member.Name.Replace("Unsaved", string.Empty, StringComparison.Ordinal).Contains(word, StringComparison.OrdinalIgnoreCase)),
                    $"{type.Name}.{member.Name} looks like a way to save; the spike is read-only.");
            }
        }

        string[] filesBefore = StorageFiles();
        Manuscript original = ExampleManuscript();
        string originalBefore = JsonSerializer.Serialize(original);
        var editor = new WorkingCopyEditorViewModel(RecordState.Load(original));

        bool unsaved = Headless.Run(() =>
        {
            using var window = Open(editor);
            window.Type(" edited");
            return editor.HasUnsavedChanges;
        });

        Assert.IsTrue(unsaved, "The edit is held as unsaved, not saved.");
        Assert.AreEqual(originalBefore, JsonSerializer.Serialize(original), "The original record was not written.");
        CollectionAssert.AreEqual(filesBefore, StorageFiles(), "No file appeared or vanished under the guarded storage root.");
        Assert.AreEqual(0, StorageEnvironment.DefaultRootUses, "No default storage root was resolved.");
    }

    private static Manuscript ExampleManuscript() =>
        ExampleLibraryService.Create(LargeLibraryFixture.Today).Manuscripts.First(item => !string.IsNullOrEmpty(item.Title));

    private static string[] StorageFiles() =>
        Directory.GetFiles(AssemblySetup.StorageRoot, "*", SearchOption.AllDirectories)
            .Select(path => path + "|" + new FileInfo(path).Length + "|" + File.GetLastWriteTimeUtc(path).Ticks)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private void TimeSnapshots(string name, IEnumerable<Manuscript> manuscripts)
    {
        Manuscript largest = manuscripts.MaxBy(item => JsonSerializer.Serialize(item).Length)!;
        RecordState record = RecordState.Load(largest);
        const int Rounds = 500;
        record.Snapshot();
        var clock = Stopwatch.StartNew();
        for (int index = 0; index < Rounds; index++)
        {
            record.Working.Title = largest.Title + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ = record.HasUnsavedChanges();
        }

        clock.Stop();
        TestContext.WriteLine(
            $"{name}: largest record serializes to {record.LastSnapshotLength:N0} characters; one keystroke's check costs {clock.Elapsed.TotalMilliseconds * 1000 / Rounds:N0} microseconds on average over {Rounds}");
    }

    private static SpikeWindow Open(WorkingCopyEditorViewModel editor) => new(editor);

    // The test window, shown and focused on its box, with the caret at the end.
    private sealed class SpikeWindow : IDisposable
    {
        private readonly WorkingCopyTestWindow _window;

        public SpikeWindow(WorkingCopyEditorViewModel editor)
        {
            _window = new WorkingCopyTestWindow { DataContext = editor };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
            Box = _window.FindControl<TextBox>("TitleBox") ?? throw new InvalidOperationException("No TitleBox.");
            UnsavedLine = _window.FindControl<TextBlock>("UnsavedLine") ?? throw new InvalidOperationException("No UnsavedLine.");
            Box.Focus(NavigationMethod.Tab);
            Box.CaretIndex = Box.Text?.Length ?? 0;
            Box.ClearSelection();
            Dispatcher.UIThread.RunJobs();
        }

        public TextBox Box { get; }

        public TextBlock UnsavedLine { get; }

        // One text-input event per character, as a keyboard sends them.
        public void Type(string text) => Keyboard.Type(_window, text);

        public void Press(Key key) => Keyboard.Press(_window, key);

        public void Dispose() => _window.Close();
    }
}
