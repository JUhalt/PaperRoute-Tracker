namespace PaperRoute.ViewModels.Board;

// Everything a card shows, worked out once a render for one manuscript
// (Form1.vb CardKey :3034-3055 and CreateManuscriptCard :3138-3149 work the
// same texts out twice; here they are worked out once and shared).
internal sealed class CardFacts
{
    private CardFacts(
        Manuscript manuscript,
        string insightText,
        CardTone insightTone,
        bool statusIsUrgent,
        string statusText,
        RouteSummary route,
        string key)
    {
        Manuscript = manuscript;
        InsightText = insightText;
        InsightTone = insightTone;
        StatusIsUrgent = statusIsUrgent;
        StatusText = statusText;
        Route = route;
        Key = key;
    }

    public Manuscript Manuscript { get; }

    public string InsightText { get; }

    public CardTone InsightTone { get; }

    public bool StatusIsUrgent { get; }

    // The insight when it is urgent, otherwise the stage clock; empty hides
    // the status line (Form1.vb:3146-3147, 3199).
    public string StatusText { get; }

    public RouteSummary Route { get; }

    public string Key { get; }

    public static CardFacts For(
        Manuscript manuscript,
        ManuscriptAttentionSnapshot snapshot,
        DateTime today,
        int fileDrawerSuggestionThreshold)
    {
        (string insightText, CardTone insightTone) = BoardRules.Insight(manuscript, snapshot, fileDrawerSuggestionThreshold);
        bool urgent = insightText.Length > 0 && insightText != BoardRules.MissingTargetInsight;
        string status = urgent ? insightText : StageClockService.Describe(manuscript, today) ?? string.Empty;
        RouteSummary route = RouteSummaryService.Describe(manuscript);
        return new CardFacts(manuscript, insightText, insightTone, urgent, status, route, KeyOf(manuscript, status, insightTone, route));
    }

    // Form1.vb CardKey (:3034-3055): title, stage, location, target journal,
    // tags, the status text, the insight's colour (here its tone), the route
    // text and the route dots, joined with U+001F. The insight's colour is
    // part of the key even when the line shows the stage clock, as today.
    private static string KeyOf(Manuscript manuscript, string status, CardTone insightTone, RouteSummary route) =>
        string.Join(
            BoardRules.StatusFieldSeparator,
            manuscript.Title,
            manuscript.CurrentStage.ToString(),
            manuscript.Location.ToString(),
            manuscript.TargetJournal,
            string.Join(",", manuscript.Tags ?? []),
            status,
            insightTone.ToString(),
            route.Text,
            string.Join(",", (route.Dots ?? []).Select(dot => dot.ToString())));
}
