using System.Diagnostics;
using System.Text;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// The trace lines written while it listens: how a test sees the working
// copy's refusal while a save writes, which logs and never throws.
internal sealed class LogCapture : TraceListener
{
    private readonly StringBuilder _partial = new();

    public List<string> Lines { get; } = [];

    public override void Write(string? message) => _partial.Append(message);

    public override void WriteLine(string? message)
    {
        _partial.Append(message);
        Lines.Add(_partial.ToString());
        _partial.Clear();
    }
}
