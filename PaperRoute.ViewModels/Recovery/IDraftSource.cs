using ManuscriptPipeline.Services;

namespace PaperRoute.ViewModels.Recovery;

// What can be kept as a recovery draft when PaperRoute closes without being
// able to ask (start-up design 2.5, build 6 D1): the open manuscript page.
// The Manuscript page's guard derives from it (IPageGuard : IDraftSource,
// #424's pull request R1), so the shell's close step and the error path can
// ask for a draft before that page exists. RecoveryDrafts.WriteFrom asks it.
public interface IDraftSource
{
    // The page's unsaved work as a draft, or null when nothing is unsaved
    // (today Form1.RecoveryDraft.vb writes only with unsaved page edits). The
    // reason is RecoveryReasons.Shutdown or RecoveryReasons.Error.
    RecoveryDraft? DraftForRecovery(string reason);
}
