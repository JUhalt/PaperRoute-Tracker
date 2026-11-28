using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace PaperRoute.Desktop.Tests.Legibility;

// R3's subjects: every kind of route mark the app draws, as the app styles it
// (harness design 2.4). Today they are the card's three route-dot states
// (Styles/Board.axaml, Ellipse.route-dot, one per RouteDotState): closed
// (8 px, margin 1, MutedTextBrush), current (10 px, AccentMutedBackgroundBrush
// fill, 1.8 px AccentBrush ring) and accepted (10 px AccentBrush). The route
// section's segment kinds join this registry; the rule does not change.
//
// Each kind is drawn alone in a fixed slot, the 10 x 10 the card gives a dot
// (10 px, or 8 px plus margin 1), on CardBackgroundBrush, at render scale 4.
// The slot is fixed because the verdict depends on it: the same two discs
// differ in about 28 % of a 10 x 10 slot but about 7 % of a 20 x 20 one.
// Limits of headless: HeadlessLimits.md
internal static class RouteKinds
{
    // The slot's side, in units.
    public const double Slot = 10;

    // The render scale the slots are drawn at.
    public const double Scale = 4;

    public static IReadOnlyList<RouteKind> All { get; } =
    [
        new("closed", () => Dot("closed")),
        new("current", () => Dot("current")),
        new("accepted", () => Dot("accepted")),
    ];

    // A route dot in one state, styled by Board.axaml as the card's template makes it.
    public static Ellipse Dot(string state) => new() { Classes = { "route-dot", state } };
}

// One kind of route mark: its name and a factory for its control.
internal sealed record RouteKind(string Name, Func<Control> Create);
