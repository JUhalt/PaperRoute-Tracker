namespace PaperRoute.ViewModels.Recovery;

// One kept draft waiting for an answer, as RecoveryDrafts.Offers lists it:
// the manuscript, its title as the draft holds it, when the draft was written
// (UTC), and, when this session saved that manuscript after the draft was
// kept, the sentence that says so (null otherwise). The window shows each as
// a notice with Open and Discard (build 6 D2).
public sealed record DraftOffer(Guid ManuscriptId, string Title, DateTime WrittenAtUtc, string? SavedAfter);
