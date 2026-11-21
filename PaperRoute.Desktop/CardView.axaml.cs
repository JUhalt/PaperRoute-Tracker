using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// One card on a shelf (d1 4.1). Its data context is a CardViewModel. The code
// here is the view's own:
// - a left click anywhere on the card opens it, except on a button, a link or
//   the tag strip, which have their own actions (Form1.vb:3352-3357, 3437-3443);
// - Space on the title opens it only bare: with Alt, Ctrl or Shift held it
//   does nothing (Form1.vb:3364-3372);
// - the stage pill's style class follows the stage;
// - the ⋯ menu and the right-click menu are filled when first opened, from
//   the card's menu items, every one off (d1 1.2).
public partial class CardView : UserControl
{
    private bool _pressed;

    public CardView()
    {
        MadeCount++;
        InitializeComponent();

        Card.AddHandler(PointerPressedEvent, OnCardPressed);
        Card.AddHandler(PointerReleasedEvent, OnCardReleased);
        Title.AddHandler(KeyDownEvent, OnTitleKeyDown, RoutingStrategies.Tunnel);
        MoreMenu = (MenuFlyout)More.Flyout!;
        RightClickMenu = (MenuFlyout)Card.ContextFlyout!;
        MoreMenu.Opening += (_, _) => Fill(MoreMenu);
        RightClickMenu.Opening += (_, _) => Fill(RightClickMenu);
    }

    // How many card views this process has made (on the interface thread),
    // so a test or a measurement can tell how many a change made (d1 4.4).
    public static int MadeCount { get; private set; }

    // The flyout ⋯ opens, and the one a right-click opens.
    public MenuFlyout MoreMenu { get; }

    public MenuFlyout RightClickMenu { get; }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        StagePill.Classes.RemoveAll(StagePill.Classes.Where(name => name.StartsWith("stage-", StringComparison.Ordinal)).ToList());
        if (DataContext is CardViewModel card)
        {
            StagePill.Classes.Add(card.StageClass);
        }

        MoreMenu.Items.Clear();
        RightClickMenu.Items.Clear();
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e) =>
        _pressed = e.GetCurrentPoint(Card).Properties.IsLeftButtonPressed && !FromOwnAction(e.Source);

    private void OnCardReleased(object? sender, PointerReleasedEventArgs e)
    {
        bool click = _pressed
            && e.InitialPressMouseButton == MouseButton.Left
            && !FromOwnAction(e.Source)
            && new Rect(Card.Bounds.Size).Contains(e.GetPosition(Card));
        _pressed = false;
        if (click && DataContext is CardViewModel card && card.OpenCommand.CanExecute(null))
        {
            e.Handled = true;
            card.OpenCommand.Execute(null);
        }
    }

    // A press on a button (the title, ⋯, View route) or on the tag strip is
    // theirs, not the card's.
    private bool FromOwnAction(object? source)
    {
        for (Visual? visual = source as Visual; visual is not null && !ReferenceEquals(visual, Card); visual = visual.GetVisualParent())
        {
            if (visual is Button or TagStripPanel)
            {
                return true;
            }
        }

        return false;
    }

    // A Button clicks on Space whatever modifier is held; today's title opens
    // on bare Space only, so a modified Space stops here.
    private void OnTitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers != KeyModifiers.None)
        {
            e.Handled = true;
        }
    }

    private void Fill(MenuFlyout flyout)
    {
        if (flyout.Items.Count > 0 || DataContext is not CardViewModel card)
        {
            return;
        }

        foreach (CardMenuItemViewModel item in card.MenuItems)
        {
            if (item.IsSeparator)
            {
                flyout.Items.Add(new Separator());
                continue;
            }

            var menuItem = new MenuItem { Header = item.Header, IsEnabled = item.IsEnabled };
            ToolTip.SetTip(menuItem, item.ToolTip);
            ToolTip.SetShowOnDisabled(menuItem, true);
            AutomationProperties.SetHelpText(menuItem, item.ToolTip);
            if (item.IsDanger)
            {
                menuItem.Classes.Add("danger");
            }

            flyout.Items.Add(menuItem);
        }
    }
}
