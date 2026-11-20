namespace PaperRoute.ViewModels.Session;

// Which library a session holds (d1 3.1): the example, or a read-only
// snapshot, the copy of a library folder a person named ("--snapshot-from",
// d1 1.3, step 11b). The real profile's snapshot ("--snapshot", step 14) is not
// built.
public enum LibraryKind
{
    Example,
    Snapshot,
}
