using System.Text.Json;

namespace PaperRoute.ViewModels.Spikes;

// The working-copy spike (d1 7.2 item 4; plan section 10, "the working-copy
// spike"). Today's edit window keeps, for the manuscript it edits, the original
// record, a working clone and a baseline snapshot of that clone
// (EditManuscriptForm.vb:20-64, the RecordState class), filled by LoadRecord
// (:417-429, the clone by ManuscriptCloneService.CloneManuscript through the
// form's own CloneManuscript, :4808-4812) and Bind (:484-485, the baseline is
// Snapshot(), or empty for a recovered draft). Snapshot() (:698-700) is
// System.Text.Json's JsonSerializer.Serialize of the working clone with the
// default options; there is no core member for it, so this calls the same
// serializer the same way. Unsaved changes are any difference between a new
// snapshot and the baseline (:611).
//
// This holds only the three parts the spike needs; the form's RecordState also
// holds the library list, the author library and its dirty flag, the deletion
// request and the parts the page builds later. It never saves: there is no
// save here, and the trial is read-only.
public sealed class RecordState
{
    private RecordState(Manuscript original, Manuscript working, bool recovered)
    {
        Original = original;
        Working = working;
        Baseline = recovered ? string.Empty : Snapshot();
    }

    // The record as the library holds it. Never written by the editor.
    public Manuscript Original { get; }

    // The clone every field edits.
    public Manuscript Working { get; }

    // The working clone as last loaded; unsaved changes are any difference from it.
    public string Baseline { get; }

    // How many snapshots have been taken, for the spike's cost question.
    public int SnapshotCount { get; private set; }

    // Characters in the last snapshot taken.
    public int LastSnapshotLength { get; private set; }

    // LoadRecord with no recovery draft: a clone of the original, and a baseline
    // of that clone. With a draft (recovered), the working clone is made from the
    // draft and the baseline matches nothing, as Bind does.
    public static RecordState Load(Manuscript original, Manuscript? recoveryDraft = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        Manuscript working = ManuscriptCloneService.CloneManuscript(recoveryDraft ?? original);
        return new RecordState(original, working, recovered: recoveryDraft is not null);
    }

    // EditManuscriptForm.Snapshot (:698-700).
    public string Snapshot()
    {
        string snapshot = JsonSerializer.Serialize(Working);
        SnapshotCount++;
        LastSnapshotLength = snapshot.Length;
        return snapshot;
    }

    // The last line of EditManuscriptForm.HasUnsavedChanges (:611). The lines
    // above it there compare the text boxes with the working clone, because a
    // WinForms box reaches the clone only at Save; a bound box writes the clone
    // as it is typed in, so the snapshot comparison is the whole check here.
    public bool HasUnsavedChanges() =>
        !string.Equals(Snapshot(), Baseline, StringComparison.Ordinal);
}
