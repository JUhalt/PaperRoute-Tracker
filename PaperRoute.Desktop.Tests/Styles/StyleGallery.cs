using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Styles;

// The style gallery (d1 8, step 6): one page that shows each family of the
// trial's styles (buttons in each state, cards, pills, chips, tags, tabs,
// inputs and the Board's text classes) for the maintainer's eye, saved as a
// picture in Light and in Dark by StyleGalleryTests.
//
// It lives in the test project, not in PaperRoute.Desktop: the CI build
// (Release) compiles and renders it like any other test, and a published
// trial cannot show it to a user, because the app does not contain it.
//
// States a pointer would make (hovered, pressed) are set as pseudo-classes:
// nothing moves the pointer in a headless window, so they stay. Focus is real,
// from the keyboard, and only one control can hold it: the button named
// "Focused". The text box and drop-down shown "focused" carry the
// pseudo-classes their focus styles select on.
internal sealed class StyleGallery : UserControl
{
    private readonly Button _focused;

    public StyleGallery()
    {
        _focused = MakeButton("Focused", []);

        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("700,28,*"),
        };
        var left = new StackPanel { Spacing = 6 };
        var right = new StackPanel { Spacing = 6 };
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(right);

        left.Children.Add(new TextBlock { Classes = { "page-title" }, Text = "Style gallery" });
        AddButtons(left);
        AddCards(left);

        AddPills(right);
        AddChips(right);
        AddTags(right);
        AddTabs(right);
        AddInputs(right);
        AddBoardText(right);

        var root = new Border
        {
            Padding = new Thickness(22, 14, 22, 14),
            Child = columns,
        };
        // As {DynamicResource}, so the page follows the window's theme.
        root.Bind(Border.BackgroundProperty, root.GetResourceObservable("BoardBackgroundBrush"));
        root.Bind(TextElement.ForegroundProperty, root.GetResourceObservable("PrimaryTextBrush"));
        root.Bind(TextElement.FontSizeProperty, root.GetResourceObservable("BodyFontSize"));
        Content = root;
    }

    // Gives the keyboard focus to the "Focused" button, so its focus ring shows.
    // Call once the window is shown.
    public void FocusTheFocusedButton() => _focused.Focus(NavigationMethod.Tab);

    private void AddButtons(StackPanel host)
    {
        host.Children.Add(Caption("BUTTONS"));
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("80,Auto,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
        };
        string[] heads = ["At rest", "Hovered", "Pressed", "Off"];
        for (int column = 0; column < heads.Length; column++)
        {
            Place(grid, new TextBlock { Classes = { "meta" }, Text = heads[column], Margin = new Thickness(0, 0, 12, 2) }, 0, column + 1);
        }

        (string Role, string Text, string[] Classes)[] roles =
        [
            ("Secondary", "Clear", []),
            ("Primary", "+ Add Manuscript", ["primary"]),
            ("Quiet", "More actions", ["quiet"]),
            ("Danger", "Delete…", ["danger"]),
        ];
        for (int row = 0; row < roles.Length; row++)
        {
            Place(grid, new TextBlock { Classes = { "meta" }, Text = roles[row].Role, VerticalAlignment = VerticalAlignment.Center }, row + 1, 0);
            Place(grid, Spaced(MakeButton(roles[row].Text, roles[row].Classes)), row + 1, 1);
            Place(grid, Spaced(With(MakeButton(roles[row].Text, roles[row].Classes), ":pointerover")), row + 1, 2);
            Place(grid, Spaced(With(MakeButton(roles[row].Text, roles[row].Classes), ":pointerover", ":pressed")), row + 1, 3);
            Button off = MakeButton(roles[row].Text, roles[row].Classes);
            off.IsEnabled = false;
            Place(grid, Spaced(off), row + 1, 4);
        }

        host.Children.Add(grid);

        var more = new WrapPanel { Orientation = Orientation.Horizontal };
        more.Children.Add(Spaced(_focused));
        more.Children.Add(Spaced(MakeButton("Compact", ["compact"])));
        more.Children.Add(Spaced(MakeButton("⋯", ["quiet", "icon"])));
        more.Children.Add(Spaced(With(MakeButton("⋯", ["quiet", "icon"]), ":pointerover")));
        more.Children.Add(Spaced(MakeButton("View in Deadlines →", ["link"])));
        more.Children.Add(Spaced(With(MakeButton("Hovered link", ["link"]), ":pointerover")));
        more.Children.Add(Spaced(With(MakeButton("Pressed link", ["link"]), ":pointerover", ":pressed")));
        host.Children.Add(more);
    }

    private static void AddCards(StackPanel host)
    {
        host.Children.Add(Caption("CARDS"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(SampleCard(
            "UNDER REVIEW", "stage-under-review", "Waiting 95 days", "warning",
            "Soil microbiomes under drought: a two-site comparison of recovery", "Journal of Examples", "secondary",
            ["closed", "closed", "current"], "3rd journal · 2 rejections", hovered: false));
        row.Children.Add(SampleCard(
            "REVISION", "stage-revision", "Revision overdue by 3 days", "danger",
            "Hovered card: a title long enough to need its second line", "No target journal yet", "warning",
            ["closed", "accepted"], "2nd journal · major revision", hovered: true));
        host.Children.Add(row);

        var callout = new Border
        {
            Classes = { "card", "callout" },
            Margin = new Thickness(0, 6, 0, 0),
            Child = new TextBlock
            {
                Text = "Callout card: your library stays on this computer. Nothing is sent anywhere unless you choose to.",
                TextWrapping = TextWrapping.Wrap,
            },
        };
        host.Children.Add(callout);

        var plain = new Border
        {
            Classes = { "card" },
            Margin = new Thickness(0, 6, 0, 0),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Plain card, with a divider below this line." },
                    new Border { Classes = { "divider" } },
                    new TextBlock { Classes = { "meta" }, Text = "Meta text under the divider" },
                },
            },
        };
        host.Children.Add(plain);
    }

    private static Border SampleCard(
        string stage, string stageClass, string status, string statusTone, string title,
        string journal, string journalTone, string[] dots, string route, bool hovered)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        top.Children.Add(Pill(stage, stageClass));
        var statusText = new TextBlock
        {
            Classes = { "status", statusTone },
            Text = status,
            Margin = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(statusText, 1);
        top.Children.Add(statusText);
        Button more = MakeButton("⋯", ["quiet", "icon"]);
        Grid.SetColumn(more, 2);
        top.Children.Add(more);

        var titleButton = new Button
        {
            Classes = { "link", "card-title" },
            Margin = new Thickness(0, 8, 0, 0),
            Height = 40,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis },
        };

        var journalRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4, 0, 0) };
        journalRow.Children.Add(new TextBlock { Classes = { "journal", journalTone }, Text = journal, VerticalAlignment = VerticalAlignment.Center });
        var tags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        tags.Children.Add(TagChip("soil", "tag-teal"));
        tags.Children.Add(TagChip("+2", "tag-slate"));
        Grid.SetColumn(tags, 1);
        journalRow.Children.Add(tags);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        var dotRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (string dot in dots)
        {
            dotRow.Children.Add(new Ellipse { Classes = { "route-dot", dot } });
        }

        footer.Children.Add(dotRow);
        var routeText = new TextBlock { Classes = { "meta" }, Text = route, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(routeText, 1);
        footer.Children.Add(routeText);
        Button view = MakeButton("View route →", ["link"]);
        view.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(view, 2);
        footer.Children.Add(view);

        var card = new Border
        {
            Classes = { "card", "interactive" },
            Width = 314,
            Child = new StackPanel
            {
                Children =
                {
                    top,
                    titleButton,
                    journalRow,
                    new Border { Classes = { "divider" }, Margin = new Thickness(0, 12, 0, 0) },
                    footer,
                },
            },
        };
        return hovered ? With(card, ":pointerover") : card;
    }

    private static void AddPills(StackPanel host)
    {
        host.Children.Add(Caption("PILLS"));
        var row = new WrapPanel();
        (string Text, string Class)[] stages =
        [
            ("IDEA", "stage-idea"), ("DRAFT", "stage-draft"), ("SUBMITTED", "stage-submitted"), ("UNDER REVIEW", "stage-under-review"),
            ("REVISION", "stage-revision"), ("ACCEPTED", "stage-accepted"), ("IN PRESS", "stage-in-press"), ("PUBLISHED", "stage-published"),
        ];
        foreach ((string text, string cls) in stages)
        {
            row.Children.Add(Spaced(Pill(text, cls)));
        }

        row.Children.Add(Spaced(Pill("3", "count")));
        row.Children.Add(Spaced(Pill("ACCENT", "accent")));
        row.Children.Add(Spaced(Pill("2", "count", "warning")));
        row.Children.Add(Spaced(Pill("1", "count", "danger")));
        host.Children.Add(row);
    }

    private static void AddChips(StackPanel host)
    {
        host.Children.Add(Caption("CHIPS (AT REST, THEN HOVERED)"));
        (string Text, string[] Classes)[] chips =
        [
            ("1 no target journal", []),
            ("2 revisions due soon", ["warning"]),
            ("1 rejected in last 30 days", ["danger"]),
            ("1 waiting 90+ days", ["warning", "active"]),
        ];
        var rest = new WrapPanel();
        var hovered = new WrapPanel();
        foreach ((string text, string[] classes) in chips)
        {
            rest.Children.Add(Spaced(Chip(text, classes)));
            hovered.Children.Add(Spaced(With(Chip(text, classes), ":pointerover")));
        }

        host.Children.Add(rest);
        host.Children.Add(hovered);

        var others = new WrapPanel();
        var toggled = new ToggleButton { Classes = { "chip" }, Content = "Toggle chip, checked", IsChecked = true };
        others.Children.Add(Spaced(toggled));
        others.Children.Add(Spaced(new ToggleButton { Classes = { "chip" }, Content = "Toggle chip" }));
        Button off = Chip("Off chip", []);
        off.IsEnabled = false;
        others.Children.Add(Spaced(off));
        host.Children.Add(others);
    }

    private static void AddTags(StackPanel host)
    {
        host.Children.Add(Caption("TAGS"));
        var row = new WrapPanel();
        foreach (string palette in new[] { "teal", "blue", "violet", "rose", "amber", "green", "slate", "orange" })
        {
            row.Children.Add(Spaced(TagChip(palette, "tag-" + palette)));
        }

        row.Children.Add(Spaced(TagChip("+3", "tag-slate")));
        host.Children.Add(row);
    }

    private static void AddTabs(StackPanel host)
    {
        host.Children.Add(Caption("TABS (PUBLISHED HOVERED)"));
        var strip = new TabStrip();
        strip.Items.Add(Tab("Pipeline", "3 of 167"));
        strip.Items.Add(With(Tab("Published", "272"), ":pointerover"));
        strip.Items.Add(Tab("File Drawer", "61"));
        strip.SelectedIndex = 0;
        var bar = new Grid();
        bar.Children.Add(new Border { Classes = { "hairline" } });
        bar.Children.Add(strip);
        host.Children.Add(bar);
    }

    private static void AddInputs(StackPanel host)
    {
        host.Children.Add(Caption("INPUTS (REST, FOCUSED, OFF)"));
        var boxes = new WrapPanel();
        boxes.Children.Add(Spaced(new TextBox { Width = 220, PlaceholderText = "Search titles, journals, authors" }));
        boxes.Children.Add(Spaced(With(new TextBox { Width = 120, Text = "drought" }, ":focus", ":focus-within")));
        boxes.Children.Add(Spaced(new TextBox { Width = 90, Text = "Off", IsEnabled = false }));
        host.Children.Add(boxes);

        var lists = new WrapPanel();
        lists.Children.Add(Spaced(Combo("All stages (7)")));
        lists.Children.Add(Spaced(With(Combo("Sort: Current order"), ":focus-within", ":focus")));
        ComboBox off = Combo("Off list");
        off.IsEnabled = false;
        lists.Children.Add(Spaced(off));
        host.Children.Add(lists);
    }

    private static void AddBoardText(StackPanel host)
    {
        host.Children.Add(Caption("BOARD TEXT"));
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        title.Children.Add(new TextBlock { Classes = { "page-title" }, Text = "Board" });
        title.Children.Add(new TextBlock { Classes = { "caption" }, Text = "NEEDS ATTENTION", VerticalAlignment = VerticalAlignment.Center });
        host.Children.Add(title);

        var statuses = new WrapPanel();
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "status", "danger" }, Text = "Revision overdue by 3 days" }));
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "status", "warning" }, Text = "Waiting 95 days" }));
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "status", "muted" }, Text = "12 days" }));
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "journal", "secondary" }, Text = "Journal of Examples" }));
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "journal", "warning" }, Text = "No target journal yet" }));
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "journal", "muted" }, Text = "No target journal" }));
        statuses.Children.Add(Spaced(new TextBlock { Classes = { "meta" }, Text = "Not yet submitted" }));
        host.Children.Add(statuses);

        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (string dot in new[] { "closed", "closed", "current", "accepted" })
        {
            dots.Children.Add(new Ellipse { Classes = { "route-dot", dot } });
        }

        var dotRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        dotRow.Children.Add(dots);
        dotRow.Children.Add(new TextBlock { Classes = { "meta" }, Text = "Route dots: closed, closed, current, accepted" });
        host.Children.Add(dotRow);

        // A shelf's empty message, as the Board draws it: a plain label on the board background (Form1.vb CreateEmptyLabel).
        host.Children.Add(new TextBlock { Classes = { "empty" }, Text = "Nothing in progress. Choose + Add Manuscript to start one." });
        host.Children.Add(new Border
        {
            Classes = { "banner" },
            Child = new TextBlock { Text = "Example library: a fictional research group's manuscripts, to explore and to teach with." },
        });
        host.Children.Add(new Border
        {
            Classes = { "notice-strip" },
            Child = new TextBlock { Text = "Notice strip: Loaded 7 manuscript(s)." },
        });
    }

    private static TextBlock Caption(string text) =>
        new() { Classes = { "caption" }, Text = text, Margin = new Thickness(0, 8, 0, 2) };

    private static Button MakeButton(string text, string[] classes)
    {
        var button = new Button { Content = text };
        button.Classes.AddRange(classes);
        return button;
    }

    private static Button Chip(string text, string[] classes)
    {
        Button chip = MakeButton(text, ["chip"]);
        chip.Classes.AddRange(classes);
        return chip;
    }

    private static Border Pill(string text, params string[] classes)
    {
        var pill = new Border { Child = new TextBlock { Text = text } };
        pill.Classes.Add("pill");
        pill.Classes.AddRange(classes);
        return pill;
    }

    private static Border TagChip(string text, string palette) =>
        new() { Classes = { "tag", palette }, Child = new TextBlock { Text = text }, VerticalAlignment = VerticalAlignment.Center };

    private static TabStripItem Tab(string name, string count) => new()
    {
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = name },
                new TextBlock { Classes = { "tab-count" }, Text = count },
            },
        },
    };

    private static ComboBox Combo(string item)
    {
        var combo = new ComboBox { Width = 190 };
        combo.Items.Add(item);
        combo.SelectedIndex = 0;
        return combo;
    }

    private static void Place(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static T Spaced<T>(T control)
        where T : Control
    {
        control.Margin = new Thickness(0, 4, 10, 4);
        return control;
    }

    // Sets pseudo-classes a pointer or focus would set.
    private static T With<T>(T control, params string[] pseudoClasses)
        where T : StyledElement
    {
        var pseudo = (IPseudoClasses)control.Classes;
        foreach (string name in pseudoClasses)
        {
            pseudo.Set(name, true);
        }

        return control;
    }
}
