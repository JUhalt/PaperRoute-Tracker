namespace PaperRoute.ViewModels.Shell;

// Where to go (shell design 2.1): a page, and for the manuscript page a
// manuscript and, optionally, one of its sections ("Route" for View route).
// History stores these, as today's PageRequest (Form1.Shell.vb:44-45).
public sealed record PageRequest(PageKey Page, Guid ManuscriptId = default, string? Section = null)
{
    // The same page, and for the manuscript page the same manuscript. The
    // section is not compared: another section of the manuscript shown is
    // not a new place, so it is no new history entry (3.1 row 17).
    public bool SamePlace(PageRequest other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Page == other.Page && ManuscriptId == other.ManuscriptId;
    }
}
