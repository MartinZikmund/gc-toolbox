using System.Diagnostics.CodeAnalysis;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The complete International Code of Signals flag set: 26 letter flags, 10 numeral pennants, the
/// answering pennant and the three substitutes. Geometry is expressed in flag-relative coordinates
/// (height = 1, width = aspect ratio) and was transcribed from the public-domain ICS flag renderings
/// on Wikimedia Commons, so the designs match the canonical charts.
/// </summary>
public static class SignalFlagsAlphabet
{
    private const double PennantAspect = 1.8;
    private const double SubstituteAspect = 550.0 / 350.0;

    /// <summary>Saltire band reach along an edge — matches the 105/600 stroke of the reference art.</summary>
    private const double SaltireOffset = 0.12375;

    private static readonly IReadOnlyList<SignalFlagPoint> Square = Points(0, 0, 1, 0, 1, 1, 0, 1);
    private static readonly IReadOnlyList<SignalFlagPoint> Swallowtail = Points(0, 0, 1, 0, 0.75, 0.5, 1, 1, 0, 1);
    private static readonly IReadOnlyList<SignalFlagPoint> Pennant = Points(0, 0, PennantAspect, 0.25, PennantAspect, 0.75, 0, 1);
    private static readonly IReadOnlyList<SignalFlagPoint> Triangle = Points(0, 0, SubstituteAspect, 0.5, 0, 1);

    private static readonly string[] LetterPhonetics =
    [
        "Alfa", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett",
        "Kilo", "Lima", "Mike", "November", "Oscar", "Papa", "Quebec", "Romeo", "Sierra", "Tango",
        "Uniform", "Victor", "Whiskey", "Xray", "Yankee", "Zulu",
    ];

    private static readonly string[] NumeralPhonetics =
    [
        "Nadazero", "Unaone", "Bissotwo", "Terrathree", "Kartefour",
        "Pantafive", "Soxisix", "Setteseven", "Oktoeight", "Novenine",
    ];

    public static IReadOnlyList<SignalFlagDescriptor> Letters { get; } = BuildLetters();

    public static IReadOnlyList<SignalFlagDescriptor> Numerals { get; } = BuildNumerals();

    public static IReadOnlyList<SignalFlagDescriptor> Specials { get; } = BuildSpecials();

    public static IReadOnlyList<SignalFlagDescriptor> All { get; } = [.. Letters, .. Numerals, .. Specials];

    /// <summary>Resolves the flag for a letter (case-insensitive) or digit.</summary>
    public static bool TryGet(char symbol, [NotNullWhen(true)] out SignalFlagDescriptor? flag)
    {
        flag = symbol switch
        {
            >= 'A' and <= 'Z' => Letters[symbol - 'A'],
            >= 'a' and <= 'z' => Letters[symbol - 'a'],
            >= '0' and <= '9' => Numerals[symbol - '0'],
            _ => null,
        };

        return flag is not null;
    }

    private static SignalFlagDescriptor[] BuildLetters()
    {
        var designs = new (IReadOnlyList<SignalFlagPoint> Outline, SignalFlagShape[] Shapes)[]
        {
            // A — white hoist half, blue fly half, swallowtailed.
            (Swallowtail, [new SignalFlagPolygon(SignalFlagColor.White, Swallowtail), Poly(SignalFlagColor.Blue, 0.5, 0, 1, 0, 0.75, 0.5, 1, 1, 0.5, 1)]),
            // B — all red, swallowtailed.
            (Swallowtail, [new SignalFlagPolygon(SignalFlagColor.Red, Swallowtail)]),
            // C — five horizontal stripes: blue, white, red, white, blue.
            (Square, [Rect(SignalFlagColor.Blue, 0, 0, 1, 1), Rect(SignalFlagColor.White, 0, 0.2, 1, 0.6), Rect(SignalFlagColor.Red, 0, 0.4, 1, 0.2)]),
            // D — yellow with a wide blue horizontal band.
            (Square, [Rect(SignalFlagColor.Yellow, 0, 0, 1, 1), Rect(SignalFlagColor.Blue, 0, 0.2, 1, 0.6)]),
            // E — blue over red.
            (Square, [Rect(SignalFlagColor.Blue, 0, 0, 1, 0.5), Rect(SignalFlagColor.Red, 0, 0.5, 1, 0.5)]),
            // F — white with a red diamond.
            (Square, [Rect(SignalFlagColor.White, 0, 0, 1, 1), Poly(SignalFlagColor.Red, 0.5, 0, 1, 0.5, 0.5, 1, 0, 0.5)]),
            // G — six vertical stripes, yellow and blue.
            (Square,
            [
                Rect(SignalFlagColor.Yellow, 0, 0, 1, 1),
                Rect(SignalFlagColor.Blue, 1 / 6.0, 0, 1 / 6.0, 1),
                Rect(SignalFlagColor.Blue, 3 / 6.0, 0, 1 / 6.0, 1),
                Rect(SignalFlagColor.Blue, 5 / 6.0, 0, 1 / 6.0, 1),
            ]),
            // H — white hoist half, red fly half.
            (Square, [Rect(SignalFlagColor.White, 0, 0, 0.5, 1), Rect(SignalFlagColor.Red, 0.5, 0, 0.5, 1)]),
            // I — yellow with a black disc.
            (Square, [Rect(SignalFlagColor.Yellow, 0, 0, 1, 1), new SignalFlagCircle(SignalFlagColor.Black, 0.5, 0.5, 0.25)]),
            // J — blue with a white horizontal band.
            (Square, [Rect(SignalFlagColor.Blue, 0, 0, 1, 1), Rect(SignalFlagColor.White, 0, 1 / 3.0, 1, 1 / 3.0)]),
            // K — yellow hoist half, blue fly half.
            (Square, [Rect(SignalFlagColor.Yellow, 0, 0, 0.5, 1), Rect(SignalFlagColor.Blue, 0.5, 0, 0.5, 1)]),
            // L — quartered yellow and black (black at top fly and bottom hoist).
            (Square,
            [
                Rect(SignalFlagColor.Yellow, 0, 0, 1, 1),
                Rect(SignalFlagColor.Black, 0.5, 0, 0.5, 0.5),
                Rect(SignalFlagColor.Black, 0, 0.5, 0.5, 0.5),
            ]),
            // M — blue with a white saltire.
            (Square, [Rect(SignalFlagColor.Blue, 0, 0, 1, 1), .. Saltire(SignalFlagColor.White)]),
            // N — blue and white 4×4 checks, blue at the top hoist.
            (Square, [Rect(SignalFlagColor.White, 0, 0, 1, 1), .. Checks(SignalFlagColor.Blue)]),
            // O — diagonal halves: red above, yellow below.
            (Square, [Rect(SignalFlagColor.Red, 0, 0, 1, 1), Poly(SignalFlagColor.Yellow, 0, 0, 0, 1, 1, 1)]),
            // P — blue with a white center square (the "Blue Peter").
            (Square, [Rect(SignalFlagColor.Blue, 0, 0, 1, 1), Rect(SignalFlagColor.White, 1 / 3.0, 1 / 3.0, 1 / 3.0, 1 / 3.0)]),
            // Q — all yellow.
            (Square, [Rect(SignalFlagColor.Yellow, 0, 0, 1, 1)]),
            // R — red with a yellow upright cross.
            (Square, [Rect(SignalFlagColor.Red, 0, 0, 1, 1), .. Cross(SignalFlagColor.Yellow, Square, 1, 0.2)]),
            // S — white with a blue center square.
            (Square, [Rect(SignalFlagColor.White, 0, 0, 1, 1), Rect(SignalFlagColor.Blue, 1 / 3.0, 1 / 3.0, 1 / 3.0, 1 / 3.0)]),
            // T — vertical thirds: red, white, blue.
            (Square,
            [
                Rect(SignalFlagColor.Red, 0, 0, 1 / 3.0, 1),
                Rect(SignalFlagColor.White, 1 / 3.0, 0, 1 / 3.0, 1),
                Rect(SignalFlagColor.Blue, 2 / 3.0, 0, 1 / 3.0, 1),
            ]),
            // U — quartered red and white (red at top hoist and bottom fly).
            (Square,
            [
                Rect(SignalFlagColor.Red, 0, 0, 1, 1),
                Rect(SignalFlagColor.White, 0.5, 0, 0.5, 0.5),
                Rect(SignalFlagColor.White, 0, 0.5, 0.5, 0.5),
            ]),
            // V — white with a red saltire.
            (Square, [Rect(SignalFlagColor.White, 0, 0, 1, 1), .. Saltire(SignalFlagColor.Red)]),
            // W — nested blue, white and red rectangles.
            (Square,
            [
                Rect(SignalFlagColor.Blue, 0, 0, 1, 1),
                Rect(SignalFlagColor.White, 0.2, 0.2, 0.6, 0.6),
                Rect(SignalFlagColor.Red, 0.4, 0.4, 0.2, 0.2),
            ]),
            // X — white with a blue upright cross.
            (Square, [Rect(SignalFlagColor.White, 0, 0, 1, 1), .. Cross(SignalFlagColor.Blue, Square, 1, 0.2)]),
            // Y — red with five yellow diagonal stripes.
            (Square, [Rect(SignalFlagColor.Red, 0, 0, 1, 1), .. DiagonalStripes(SignalFlagColor.Yellow)]),
            // Z — diagonal quarters: yellow top, black hoist, red bottom, blue fly.
            (Square,
            [
                Rect(SignalFlagColor.Yellow, 0, 0, 1, 1),
                Poly(SignalFlagColor.Black, 0, 0, 0.5, 0.5, 0, 1),
                Poly(SignalFlagColor.Red, 0, 1, 0.5, 0.5, 1, 1),
                Poly(SignalFlagColor.Blue, 1, 1, 0.5, 0.5, 1, 0),
            ]),
        };

        return [.. designs.Select((design, index) =>
        {
            var letter = (char)('A' + index);
            return new SignalFlagDescriptor(
                letter.ToString(), letter, SignalFlagKind.Letter, LetterPhonetics[index],
                CaptionKey: null, MeaningKey: $"SignalFlagsMeaning{letter}",
                AspectRatio: 1, design.Outline, design.Shapes);
        })];
    }

    private static SignalFlagDescriptor[] BuildNumerals()
    {
        var designs = new SignalFlagShape[][]
        {
            // 0 — yellow with a red vertical band.
            [Field(SignalFlagColor.Yellow), Band(SignalFlagColor.Red, 0.6, 0, 0.6, 1)],
            // 1 — white with a red disc at the hoist.
            [Field(SignalFlagColor.White), new SignalFlagCircle(SignalFlagColor.Red, 0.5, 0.5, 0.25)],
            // 2 — blue with a white disc at the hoist.
            [Field(SignalFlagColor.Blue), new SignalFlagCircle(SignalFlagColor.White, 0.5, 0.5, 0.25)],
            // 3 — vertical thirds: red, white, blue.
            [Field(SignalFlagColor.White), Band(SignalFlagColor.Red, 0, 0, 0.6, 1), Band(SignalFlagColor.Blue, 1.2, 0, 0.6, 1)],
            // 4 — red with a white cross.
            [Field(SignalFlagColor.Red), .. Cross(SignalFlagColor.White, Pennant, PennantAspect, 0.25)],
            // 5 — yellow hoist half, blue fly half.
            [Field(SignalFlagColor.Yellow), Band(SignalFlagColor.Blue, 0.9, 0, 0.9, 1)],
            // 6 — black over white.
            [Field(SignalFlagColor.Black), Band(SignalFlagColor.White, 0, 0.5, PennantAspect, 0.5)],
            // 7 — yellow over red.
            [Field(SignalFlagColor.Yellow), Band(SignalFlagColor.Red, 0, 0.5, PennantAspect, 0.5)],
            // 8 — white with a red cross.
            [Field(SignalFlagColor.White), .. Cross(SignalFlagColor.Red, Pennant, PennantAspect, 0.25)],
            // 9 — quarters: white, black, red, yellow.
            [
                Field(SignalFlagColor.Red),
                Band(SignalFlagColor.White, 0, 0, 0.9, 0.5),
                Band(SignalFlagColor.Black, 0.9, 0, 0.9, 0.5),
                Band(SignalFlagColor.Yellow, 0.9, 0.5, 0.9, 0.5),
            ],
        };

        return [.. designs.Select((shapes, digit) => new SignalFlagDescriptor(
            digit.ToString(), (char)('0' + digit), SignalFlagKind.Numeral, NumeralPhonetics[digit],
            CaptionKey: null, MeaningKey: null,
            PennantAspect, Pennant, shapes))];

        static SignalFlagPolygon Field(SignalFlagColor color) => new(color, Pennant);
        static SignalFlagPolygon Band(SignalFlagColor color, double x, double y, double width, double height)
            => ClippedRect(color, Pennant, x, y, width, height);
    }

    private static SignalFlagDescriptor[] BuildSpecials()
    {
        SignalFlagPolygon triangleField = new(SignalFlagColor.Blue, Triangle);

        return
        [
            // Answering pennant — five vertical stripes, red and white.
            new("Answer", null, SignalFlagKind.Answer, string.Empty, "SignalFlagsAnswerPennant", "SignalFlagsMeaningAnswer",
                PennantAspect, Pennant,
                [
                    new SignalFlagPolygon(SignalFlagColor.Red, Pennant),
                    ClippedRect(SignalFlagColor.White, Pennant, 0.36, 0, 0.36, 1),
                    ClippedRect(SignalFlagColor.White, Pennant, 1.08, 0, 0.36, 1),
                ]),
            // First substitute — yellow with a blue border open at the hoist.
            new("Substitute1", null, SignalFlagKind.Substitute, string.Empty, "SignalFlagsSubstitute1", "SignalFlagsMeaningSubstitute1",
                SubstituteAspect, Triangle,
                [triangleField, Poly(SignalFlagColor.Yellow, 0, 1 / 6.0, SubstituteAspect * 2 / 3, 0.5, 0, 5 / 6.0)]),
            // Second substitute — blue hoist half, white fly tip.
            new("Substitute2", null, SignalFlagKind.Substitute, string.Empty, "SignalFlagsSubstitute2", "SignalFlagsMeaningSubstitute2",
                SubstituteAspect, Triangle,
                [
                    ClippedRect(SignalFlagColor.Blue, Triangle, 0, 0, SubstituteAspect / 2, 1),
                    ClippedRect(SignalFlagColor.White, Triangle, SubstituteAspect / 2, 0, SubstituteAspect / 2, 1),
                ]),
            // Third substitute — white with a black horizontal band.
            new("Substitute3", null, SignalFlagKind.Substitute, string.Empty, "SignalFlagsSubstitute3", "SignalFlagsMeaningSubstitute3",
                SubstituteAspect, Triangle,
                [
                    new SignalFlagPolygon(SignalFlagColor.White, Triangle),
                    ClippedRect(SignalFlagColor.Black, Triangle, 0, 115 / 350.0, SubstituteAspect, 120 / 350.0),
                ]),
        ];
    }

    private static SignalFlagPoint[] Points(params double[] xy)
    {
        var points = new SignalFlagPoint[xy.Length / 2];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new(xy[2 * i], xy[(2 * i) + 1]);
        }

        return points;
    }

    private static SignalFlagPolygon Poly(SignalFlagColor color, params double[] xy) => new(color, Points(xy));

    private static SignalFlagPolygon Rect(SignalFlagColor color, double x, double y, double width, double height)
        => Poly(color, x, y, x + width, y, x + width, y + height, x, y + height);

    /// <summary>An upright cross of the given arm thickness, centered and clipped to the flag outline.</summary>
    private static SignalFlagPolygon[] Cross(SignalFlagColor color, IReadOnlyList<SignalFlagPoint> outline, double width, double arm)
        =>
        [
            ClippedRect(color, outline, 0, 0.5 - (arm / 2), width, arm),
            ClippedRect(color, outline, (width - arm) / 2, 0, arm, 1),
        ];

    /// <summary>A corner-to-corner diagonal cross on the unit square, matching the reference stroke width.</summary>
    private static SignalFlagPolygon[] Saltire(SignalFlagColor color)
    {
        const double d = SaltireOffset;
        return
        [
            Poly(color, 0, 0, d, 0, 1, 1 - d, 1, 1, 1 - d, 1, 0, d),
            Poly(color, 1 - d, 0, 1, 0, 1, d, d, 1, 0, 1, 0, 1 - d),
        ];
    }

    /// <summary>The 8 colored squares of a 4×4 checkerboard, colored at the top hoist.</summary>
    private static SignalFlagPolygon[] Checks(SignalFlagColor color)
    {
        List<SignalFlagPolygon> checks = new(8);
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                if ((row + column) % 2 == 0)
                {
                    checks.Add(Rect(color, column * 0.25, row * 0.25, 0.25, 0.25));
                }
            }
        }

        return [.. checks];
    }

    /// <summary>Five 45° stripes (bendy sinister of ten) clipped to the unit square.</summary>
    private static SignalFlagPolygon[] DiagonalStripes(SignalFlagColor color)
    {
        List<SignalFlagPolygon> stripes = new(5);
        for (var stripe = 0; stripe < 5; stripe++)
        {
            // Stripe bands run along lines of constant x + y, 0.2 apart over [0, 2].
            var from = stripe * 0.4;
            var clipped = ClipHalfPlane(Square, 1, 1, from + 0.2);
            clipped = ClipHalfPlane(clipped, -1, -1, -from);
            stripes.Add(new(color, clipped));
        }

        return [.. stripes];
    }

    /// <summary>An axis-aligned rectangle clipped to a convex flag outline (pennants taper toward the fly).</summary>
    private static SignalFlagPolygon ClippedRect(SignalFlagColor color, IReadOnlyList<SignalFlagPoint> outline, double x, double y, double width, double height)
    {
        var clipped = ClipHalfPlane(outline, -1, 0, -x);
        clipped = ClipHalfPlane(clipped, 1, 0, x + width);
        clipped = ClipHalfPlane(clipped, 0, -1, -y);
        clipped = ClipHalfPlane(clipped, 0, 1, y + height);
        return new(color, clipped);
    }

    /// <summary>Sutherland–Hodgman step: keeps the part of the polygon where a·x + b·y ≤ c.</summary>
    private static IReadOnlyList<SignalFlagPoint> ClipHalfPlane(IReadOnlyList<SignalFlagPoint> points, double a, double b, double c)
    {
        List<SignalFlagPoint> result = new(points.Count + 2);
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            var currentValue = (a * current.X) + (b * current.Y);
            var nextValue = (a * next.X) + (b * next.Y);

            if (currentValue <= c)
            {
                result.Add(current);
            }

            if (currentValue <= c != nextValue <= c)
            {
                var t = (c - currentValue) / (nextValue - currentValue);
                result.Add(new(current.X + (t * (next.X - current.X)), current.Y + (t * (next.Y - current.Y))));
            }
        }

        return result;
    }
}
