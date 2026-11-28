namespace PaperRoute.Desktop.Platform;

// Why keys cannot be kept on this computer (start-up design 2.7, N2). The
// core's ISecretProtector says only whether they can (IsAvailable); the
// protectors beside DPAPI also say why not, for Settings to show (#435).
public interface IExplainsAvailability
{
    // Null when keys can be kept; otherwise one sentence for the person.
    string? WhyNotAvailable { get; }
}
