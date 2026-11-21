namespace PaperRoute.ViewModels.Board;

// Which of d1 4.4's two shelf layouts the trial draws, behind one constant
// (design step 13):
// - "a": every card of a shown shelf is made at once, in a wrapping panel;
// - "b": rows of N cards in a virtualizing list, so only the rows on screen,
//   and a small margin, are made (CardRowViewModel, ShelfView).
// Option (b) is the default. A build with the MSBuild property ShelfOption=a
// (for example "dotnet test -p:ShelfOption=a") defines SHELF_OPTION_A and
// draws option (a), so both can be built, tested and measured.
public static class ShelfLayout
{
#if SHELF_OPTION_A
    public const string Option = "a";
#else
    public const string Option = "b";
#endif

    // True under option (b). A property, not a constant, so code for the
    // other option is not unreachable to the compiler.
    public static bool UsesRows => Option == "b";
}
