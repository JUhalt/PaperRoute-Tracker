using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// One shelf of the Board (d1 4.1, 4.4). Its data context is a ShelfViewModel.
// The view reports the shelf's width; the view model works out the columns
// and the width of a card from it.
public partial class ShelfView : UserControl
{
    public ShelfView()
    {
        InitializeComponent();
        Scroller.SizeChanged += (_, e) => (DataContext as ShelfViewModel)?.SetWidth(e.NewSize.Width);
        DataContextChanged += (_, _) => (DataContext as ShelfViewModel)?.SetWidth(Scroller.Bounds.Width);
    }

    // The card views made so far, in shelf order.
    public IReadOnlyList<CardView> CardViews() =>
        Cards.GetVisualDescendants().OfType<CardView>().ToList();

    // Puts the keyboard on the first card's title, as Tab would. False when
    // the shelf shows no card.
    public bool FocusFirstCard()
    {
        Cards.UpdateLayout();
        CardView? first = CardViews().FirstOrDefault();
        return first?.FindControl<Button>("Title")?.Focus(NavigationMethod.Tab) ?? false;
    }
}
