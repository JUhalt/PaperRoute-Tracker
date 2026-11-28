using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Recovery;

namespace PaperRoute.Desktop.Tests.Recovery;

// A stand-in for the Manuscript page as a draft source (start-up design 2.5,
// build 6 D1): it gives a draft of one manuscript with a fixed time, nothing
// (nothing unsaved), or throws, and records each reason it was asked with.
internal sealed class TestDraftPage : IDraftSource
{
    private readonly Guid _id;
    private readonly string _title;
    private readonly DateTime _writtenAtUtc;

    public TestDraftPage(Guid id, string title, DateTime writtenAtUtc)
    {
        _id = id;
        _title = title;
        _writtenAtUtc = writtenAtUtc;
    }

    // Nothing is unsaved: DraftForRecovery returns null.
    public bool NothingUnsaved { get; set; }

    // DraftForRecovery throws this.
    public Exception? Throws { get; set; }

    public List<string> Reasons { get; } = [];

    public RecoveryDraft? DraftForRecovery(string reason)
    {
        Reasons.Add(reason);
        if (Throws is not null)
        {
            throw Throws;
        }

        return NothingUnsaved ? null : Draft(_id, _title, _writtenAtUtc, reason);
    }

    // A draft as the page would give it.
    public static RecoveryDraft Draft(Guid id, string title, DateTime writtenAtUtc, string reason = "test") => new()
    {
        ManuscriptId = id,
        WrittenAtUtc = writtenAtUtc,
        Reason = reason,
        Manuscript = new Manuscript { Id = id, Title = title },
    };
}
