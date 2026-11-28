using ManuscriptPipeline.Services;

namespace PaperRoute.ViewModels.Recovery;

// The offer of a kept draft to the Manuscript page (start-up design 2.5,
// build 6 D1; the frame design's IRecoveryOffer). The frame asks before every
// load, by every route of opening, so a waiting draft is shown first; the
// page then adopts it.
public interface IRecoveryOffer
{
    // The newest waiting draft an earlier session kept for this manuscript,
    // or null: none, already adopted or discarded in this session, or its
    // manuscript is not in the library.
    RecoveryDraft? PendingDraftFor(Guid manuscriptId);

    // The page put that draft on the page as unsaved changes: it is now this
    // session's and is no longer offered. Its file stays until the page saves
    // or discards, so a crash between opening and saving loses nothing.
    void Adopt(Guid manuscriptId);
}
