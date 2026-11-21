using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.Board;

// One tag on a card, with its colour: the colour chosen in the author
// library, or one from the tag's name (WorkTypeService.ColorOf, the core's).
public sealed partial class TagViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaletteClass))]
    private TagPalette _palette;

    public TagViewModel(string text, TagPalette palette)
    {
        Text = text ?? string.Empty;
        Palette = palette;
    }

    public string Text { get; }

    // The style class of the palette: "tag-teal" to "tag-orange".
    public string PaletteClass => "tag-" + Palette.ToString().ToLowerInvariant();

    public override string ToString() => Text;
}
