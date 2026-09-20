# Manuscript shelf layout verification — 2026-09-20

This record covers the narrow/high-DPI shelf follow-up tracked in [issue #37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37), brought forward alongside the v0.5 development increment. It is disposable UI evidence, not a release certification.

## Defect and fix

After a wide-to-narrow restore, a shelf could retain a display width of about 1772 pixels while its viewport was about 923 pixels wide. The cards themselves had already resized to about 882 pixels, so the horizontal scrollbar led into empty space. The production fix uses a vertical shelf flow and a shelf-specific second layout pass to reconcile the native scroll range after cards are resized or replaced. Genuine vertical overflow remains available. Lower shelves no longer force a full card height on their parent layout; each shelf can use its own vertical viewport and scrollbar at the minimum window.

## Evidence

- Focused shelf regressions: **10/10 passed** before the minimum-height refinement, including wide/narrow cycles, maximize/restore, empty/populated replacement, action bounds, and last-card reachability.
- Focused minimum-height and overflow regressions: **8/8 passed** after the refinement at the supported layout stress sizes.
- Final shelf worktree full suite after the minimum-height refinement: **486/486 passed, 0 failed, 0 skipped**. TRX: `TestResults/shelf-final-full-after-minheight.trx`.
- Native 100% (DeviceDpi 96) and 125% (DeviceDpi 120) demo checks passed through minimum → maximize → restore with vertical scrolling retained and no horizontal shelf scrollbars. At 125%, the pre-fix restored display width was 1772 pixels; the fixed restored width was 915 pixels inside a 923-pixel viewport.
- Native 150% (DeviceDpi 144) final verification passed after the minimum-height refinement. The minimum board window showed vertical shelf scrolling with no horizontal shelf range; the last Pipeline card was reachable by scrolling, and its `View route` action opened successfully.

Evidence files are retained in the ignored shelf worktree `TestResults` directory, including `shelf-native-125-before.json`, `shelf-native-125-after.json`, and `shelf-native-100-after.json`. No normal PaperRoute library is used by the demo.
