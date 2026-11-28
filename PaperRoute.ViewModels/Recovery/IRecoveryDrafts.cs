namespace PaperRoute.ViewModels.Recovery;

// What the Manuscript page's frame calls on the recovery drafts (start-up
// design 2.5, build 6 D1): the offer, and the two ends of a page's work. Each
// deletes only a draft this session owns: one it wrote or adopted. A draft an
// earlier session kept and nobody answered stays on disk.
public interface IRecoveryDrafts : IRecoveryOffer
{
    // The frame's save step: deletes this session's draft for that
    // manuscript. A waiting draft of an earlier session is kept, and its offer
    // then says that a save came after it.
    void PageSaved(Guid manuscriptId);

    // Discard on the page, or leaving it with No: the same delete.
    void PageDiscarded(Guid manuscriptId);
}
