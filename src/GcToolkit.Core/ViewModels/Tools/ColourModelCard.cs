using System.Collections.Generic;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One colour-model card in the grid (Hexadecimal, RGB, CMYK, …): a localized <see cref="TitleKey"/>
/// and the ordered <see cref="Fields"/> it edits. <see cref="Parse"/> turns the card's current field
/// values into the canonical <see cref="Alphabets.RgbColour"/> (or <see langword="null"/> while the
/// fields aren't yet a valid colour); <see cref="Write"/> formats a colour back into the fields.
/// </summary>
public sealed class ColourModelCard
{
    private readonly Func<Alphabets.RgbColour?> _parse;
    private readonly Action<Alphabets.RgbColour> _write;

    public ColourModelCard(
        string titleKey,
        IReadOnlyList<ColourField> fields,
        Func<Alphabets.RgbColour?> parse,
        Action<Alphabets.RgbColour> write)
    {
        TitleKey = titleKey;
        Fields = fields;
        _parse = parse;
        _write = write;
    }

    /// <summary>Localization key for the card heading (e.g. <c>ColourConversionRgb</c>).</summary>
    public string TitleKey { get; }

    public IReadOnlyList<ColourField> Fields { get; }

    public Alphabets.RgbColour? Parse() => _parse();

    public void Write(Alphabets.RgbColour colour) => _write(colour);
}
