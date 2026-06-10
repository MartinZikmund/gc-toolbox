namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One International Code of Signals flag: its identity, ICS phonetic name, localization keys and a
/// resolution-independent vector description (geometry verified against the public-domain ICS flag
/// renderings on Wikimedia Commons).
/// </summary>
/// <param name="Id">Stable identifier — the letter/digit, or <c>Answer</c> / <c>Substitute1..3</c>.</param>
/// <param name="Symbol">The character this flag encodes, or <see langword="null"/> for the display-only specials.</param>
/// <param name="Kind">The flag family (letter, numeral pennant, answering pennant, substitute).</param>
/// <param name="PhoneticName">Invariant ICS spelling alphabet name (Alfa, Bravo, … Nadazero, …); empty for specials.</param>
/// <param name="CaptionKey">Localization key of the display caption for flags without a symbol; otherwise <see langword="null"/>.</param>
/// <param name="MeaningKey">Localization key of the single-flag ICS meaning, or <see langword="null"/> when none is assigned.</param>
/// <param name="AspectRatio">Width over height: 1 for letter flags, &gt;1 for pennants.</param>
/// <param name="Outline">The flag silhouette in flag-relative coordinates (height = 1, width = <paramref name="AspectRatio"/>).</param>
/// <param name="Shapes">The solid-color shapes making up the design, painted in order.</param>
public sealed partial record SignalFlagDescriptor(
    string Id,
    char? Symbol,
    SignalFlagKind Kind,
    string PhoneticName,
    string? CaptionKey,
    string? MeaningKey,
    double AspectRatio,
    IReadOnlyList<SignalFlagPoint> Outline,
    IReadOnlyList<SignalFlagShape> Shapes);
