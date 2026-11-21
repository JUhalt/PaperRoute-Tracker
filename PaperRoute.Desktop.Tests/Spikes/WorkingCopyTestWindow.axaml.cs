using Avalonia.Controls;

namespace PaperRoute.Desktop.Tests.Spikes;

// The working-copy spike's headless test window. Its data context is a
// WorkingCopyEditorViewModel.
public partial class WorkingCopyTestWindow : Window
{
    public WorkingCopyTestWindow()
    {
        InitializeComponent();
    }
}
