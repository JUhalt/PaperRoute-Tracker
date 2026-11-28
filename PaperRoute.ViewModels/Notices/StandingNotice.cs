using System.Windows.Input;

namespace PaperRoute.ViewModels.Notices;

// How serious a standing notice is. The strip shows Failure first, then
// Warning, then Info (shell design 2.4); the order of the values is that rank.
public enum NoticeKind
{
    Info,
    Warning,
    Failure,
}

// A button on a standing notice: its label names it (shell design 6).
public sealed record NoticeAction
{
    public NoticeAction(string label, ICommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(command);
        Label = label;
        Command = command;
    }

    public string Label { get; }

    public ICommand Command { get; }
}

// A notice that stays in the notice strip until it is withdrawn (shell design
// 2.4), optionally with buttons, where today a message with buttons is a box.
// Its key names it: a notice shown again under the same key replaces the old
// one, and Withdraw takes the key. Its text names it for a screen reader, so
// the text is never empty. Nothing shows one yet; its first users are the
// storage failure (#421), the startup reminder on Linux (#432), the update
// notice (#435) and the failed-save warning, when the save comes.
public sealed record StandingNotice
{
    public StandingNotice(string key, string text, NoticeKind kind, IReadOnlyList<NoticeAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(actions);
        Key = key;
        Text = text;
        Kind = kind;
        Actions = actions;
    }

    public string Key { get; }

    public string Text { get; }

    public NoticeKind Kind { get; }

    public IReadOnlyList<NoticeAction> Actions { get; }
}
