using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// A card's tags on one line (d1 4.2; today's TagStrip, Controls\TagChips.vb:
// 70-154): as many chips as fit in the width it is given, then "+N" in the
// Slate palette for the rest (PlaceChips, :107-130). The card gives it at most
// half its journal line (JournalLinePanel). Each chip is a Border.tag in its
// palette's class (Chip.axaml) with the text inside, padded 7,2, 4 px apart.
//
// It is one chip tall even with no tags, so every card keeps one height (d1
// 4.1). It is not a tab stop; a left click anywhere on it runs Command, which
// puts the card's first tag in the search box whichever chip was clicked
// (Form1.vb:3285-3289), and the click goes no further, so the card does not
// open (Form1.vb:3440).
public sealed class TagStripPanel : Panel
{
    public const string MoreClass = "tag-more";

    public static readonly StyledProperty<IReadOnlyList<TagViewModel>?> TagsProperty =
        AvaloniaProperty.Register<TagStripPanel, IReadOnlyList<TagViewModel>?>(nameof(Tags));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<TagStripPanel, ICommand?>(nameof(Command));

    // The gap between chips (TagChips.vb:111).
    private const double Gap = 4;

    private readonly Border _more;
    private readonly TextBlock _moreText;
    private readonly List<(Border Chip, TagViewModel Tag)> _chips = [];

    // Every chip made, shown or not, in the order of Children.
    private readonly List<Border> _made = [];
    private readonly List<(Control Chip, double X)> _placed = [];
    private bool _attached;
    private bool _pressed;

    public TagStripPanel()
    {
        Focusable = false;
        IsTabStop = false;
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);

        _moreText = new TextBlock();
        _more = new Border { Child = _moreText };
        _more.Classes.Add("tag");
        _more.Classes.Add("tag-slate");
        _more.Classes.Add(MoreClass);
        Children.Add(_more);
    }

    public IReadOnlyList<TagViewModel>? Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    // The chips drawn, in order, with their texts ("+N" last when some do
    // not fit). For tests.
    public IReadOnlyList<string> ShownTexts =>
        _placed.Select(placed => ((TextBlock)((Border)placed.Chip).Child!).Text ?? string.Empty).ToList();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TagsProperty)
        {
            Unwatch();
            Rebuild();
            Watch();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Unwatch();
        _attached = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var unlimited = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double width = availableSize.Width;
        _placed.Clear();

        // One chip tall, whatever is shown.
        _moreText.Text = "+0";
        _more.Measure(unlimited);
        double height = _more.DesiredSize.Height;

        double x = 0;
        for (int index = 0; index < _chips.Count; index++)
        {
            Border chip = _chips[index].Chip;
            chip.Measure(unlimited);
            double chipWidth = chip.DesiredSize.Width;
            height = Math.Max(height, chip.DesiredSize.Height);
            int remaining = _chips.Count - index - 1;
            double moreWidth = remaining > 0 ? MoreWidth(remaining, unlimited) + Gap : 0;
            if (x + chipWidth + moreWidth > width)
            {
                double lastWidth = MoreWidth(_chips.Count - index, unlimited);
                if (x + lastWidth <= width)
                {
                    _placed.Add((_more, x));
                    x += lastWidth + Gap;
                }

                break;
            }

            _placed.Add((chip, x));
            x += chipWidth + Gap;
        }

        if (!_placed.Any(placed => ReferenceEquals(placed.Chip, _more)))
        {
            _moreText.Text = "+0";
            _more.Measure(unlimited);
        }

        double shown = _placed.Count == 0 ? 0 : _placed[^1].X + _placed[^1].Chip.DesiredSize.Width;
        return new Size(shown, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (Control child in Children)
        {
            if (!_placed.Any(placed => ReferenceEquals(placed.Chip, child)))
            {
                child.Arrange(default);
            }
        }

        foreach ((Control chip, double x) in _placed)
        {
            double chipHeight = Math.Min(chip.DesiredSize.Height, finalSize.Height);
            chip.Arrange(new Rect(x, (finalSize.Height - chipHeight) / 2, chip.DesiredSize.Width, chipHeight));
        }

        return finalSize;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        bool click = _pressed && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this));
        _pressed = false;
        if (!click)
        {
            return;
        }

        e.Handled = true;
        if (Command is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    private double MoreWidth(int count, Size unlimited)
    {
        _moreText.Text = "+" + count.ToString(CultureInfo.CurrentCulture);
        _more.Measure(unlimited);
        return _more.DesiredSize.Width;
    }

    // Shows Tags with the chips the strip has made already, making more only
    // when it has too few (a card view is given card after card as rows of N
    // refill it). The chips are the first children, in order, then "+N"; a
    // chip not needed now stays, hidden, between them and "+N" for a later
    // card. When the new tags read and colour as the old ones, nothing is
    // touched and nothing is measured again.
    private void Rebuild()
    {
        IReadOnlyList<TagViewModel> tags = Tags ?? [];
        if (ShowsAlready(tags))
        {
            for (int index = 0; index < tags.Count; index++)
            {
                _chips[index] = (_chips[index].Chip, tags[index]);
            }

            return;
        }

        _chips.Clear();
        for (int index = 0; index < tags.Count; index++)
        {
            TagViewModel tag = tags[index];
            Border chip;
            if (index < _made.Count)
            {
                chip = _made[index];
                var text = (TextBlock)chip.Child!;
                if (!string.Equals(text.Text, tag.Text, StringComparison.Ordinal))
                {
                    text.Text = tag.Text;
                }

                SetPalette(chip, tag.PaletteClass);
                chip.IsVisible = true;
            }
            else
            {
                chip = new Border { Child = new TextBlock { Text = tag.Text } };
                chip.Classes.Add("tag");
                chip.Classes.Add(tag.PaletteClass);
                Children.Insert(_made.Count, chip);
                _made.Add(chip);
            }

            _chips.Add((chip, tag));
        }

        for (int index = tags.Count; index < _made.Count; index++)
        {
            _made[index].IsVisible = false;
        }

        InvalidateMeasure();
    }

    // Whether the chips shown are these tags already: the same texts in the
    // same palettes, in order.
    private bool ShowsAlready(IReadOnlyList<TagViewModel> tags)
    {
        if (tags.Count != _chips.Count)
        {
            return false;
        }

        for (int index = 0; index < tags.Count; index++)
        {
            Border chip = _chips[index].Chip;
            if (!string.Equals(((TextBlock)chip.Child!).Text, tags[index].Text, StringComparison.Ordinal)
                || !chip.Classes.Contains(tags[index].PaletteClass))
            {
                return false;
            }
        }

        return true;
    }

    // A chip's palette class, the one class of it that starts "tag-".
    private static void SetPalette(Border chip, string palette)
    {
        if (chip.Classes.Contains(palette))
        {
            return;
        }

        chip.Classes.RemoveAll(chip.Classes.Where(name => name.StartsWith("tag-", StringComparison.Ordinal)).ToList());
        chip.Classes.Add(palette);
    }

    // A kept card's tag colours follow the author library (CardViewModel.Refresh).
    private void Watch()
    {
        if (_attached)
        {
            foreach ((_, TagViewModel tag) in _chips)
            {
                tag.PropertyChanged += OnTagChanged;
            }
        }
    }

    private void Unwatch()
    {
        foreach ((_, TagViewModel tag) in _chips)
        {
            tag.PropertyChanged -= OnTagChanged;
        }
    }

    private void OnTagChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TagViewModel.PaletteClass))
        {
            return;
        }

        foreach ((Border chip, TagViewModel tag) in _chips)
        {
            if (ReferenceEquals(tag, sender))
            {
                SetPalette(chip, tag.PaletteClass);
            }
        }
    }
}

// A card's journal line (d1 4.1, row 2; Form1.vb layoutCard :3460-3467): the
// journal at the left and the tag strip right-aligned, the strip given at most
// half the line and the journal the rest, 8 px apart when there are tags. The
// first child is the journal, the second the strip.
//
// A panel of its own, not d1's Grid "*,Auto": a Grid measures an Auto column
// with no width limit, so the strip could not know half the line.
public sealed class JournalLinePanel : Panel
{
    private const double Gap = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        (Control? journal, Control? strip) = Parts();
        double width = availableSize.Width;
        double stripWidth = 0;
        double height = 0;
        if (strip is not null)
        {
            double half = double.IsInfinity(width) ? width : Math.Floor(width / 2);
            strip.Measure(new Size(half, availableSize.Height));
            stripWidth = strip.DesiredSize.Width;
            height = strip.DesiredSize.Height;
        }

        double gap = stripWidth > 0 ? Gap : 0;
        if (journal is not null)
        {
            journal.Measure(new Size(Math.Max(0, width - stripWidth - gap), availableSize.Height));
            height = Math.Max(height, journal.DesiredSize.Height);
        }

        double desired = double.IsInfinity(width) ? (journal?.DesiredSize.Width ?? 0) + gap + stripWidth : width;
        return new Size(desired, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        (Control? journal, Control? strip) = Parts();
        double stripWidth = Math.Min(strip?.DesiredSize.Width ?? 0, finalSize.Width);
        double gap = stripWidth > 0 ? Gap : 0;
        strip?.Arrange(new Rect(finalSize.Width - stripWidth, 0, stripWidth, finalSize.Height));
        if (journal is not null)
        {
            double journalHeight = Math.Min(journal.DesiredSize.Height, finalSize.Height);
            journal.Arrange(new Rect(0, (finalSize.Height - journalHeight) / 2, Math.Max(0, finalSize.Width - stripWidth - gap), journalHeight));
        }

        return finalSize;
    }

    private (Control? Journal, Control? Strip) Parts() =>
        (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null);
}
