namespace PaperRoute.ViewModels.Shell;

// A page being built (shell design 2.1): the plain page that stands for one a
// later issue builds, with the page's title and that issue. It keeps nothing
// and never refuses a leave.
public sealed class PageBeingBuiltViewModel : IPage
{
    public PageBeingBuiltViewModel(string title, string issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue);
        Title = title;
        Issue = issue;
    }

    public string Title { get; }

    // The GitHub issue that builds the page: "#430".
    public string Issue { get; }
}
