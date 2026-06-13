using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Positional keyboard-layout remapper: interprets text as if typed on one physical layout and
/// re-emits each character at the same physical key position on another layout — any-to-any across
/// QWERTY and the three Dvorak variants. This is the cipher behind geocachingtoolbox.com's
/// "Dvorak keyboard" tool (which offers Qwerty, Dvorak two hands, Dvorak right hand, Dvorak left hand).
/// </summary>
/// <remarks>
/// Layouts are sourced from the official Wikipedia keyboard diagrams and transcribed key-by-key in a
/// single shared physical-key order (US ANSI, 47 keys over four rows: number row, then the QWERTY
/// <c>q…\</c>, <c>a…'</c>, <c>z…/</c> rows). Each key carries its unshifted and shifted glyph so case
/// and symbols round-trip:
/// <list type="bullet">
///   <item><description>Simplified (two-handed) Dvorak — <c>commons.wikimedia.org/wiki/File:KB_United_States_Dvorak.svg</c></description></item>
///   <item><description>One-handed right-hand Dvorak — <c>commons.wikimedia.org/wiki/File:KB_Dvorak_Right.svg</c></description></item>
///   <item><description>One-handed left-hand Dvorak — <c>commons.wikimedia.org/wiki/File:KB_Dvorak_Left.svg</c></description></item>
/// </list>
/// <see cref="Remap"/> finds each input character's physical key in the source layout and emits the
/// character at that same position on the target layout, preserving shifted vs. unshifted. Characters
/// with no key on the source layout (spaces, accented letters, etc.) pass through unchanged.
/// </remarks>
public sealed class DvorakCodec
{
    // The shared physical-key order. Comments show the corresponding QWERTY key for each column.
    // ----- QWERTY (the reference) -----
    private static readonly DvorakKey[] QwertyKeys =
    [
        // Number row: ` 1 2 3 4 5 6 7 8 9 0 - =
        new('`', '~'), new('1', '!'), new('2', '@'), new('3', '#'), new('4', '$'), new('5', '%'),
        new('6', '^'), new('7', '&'), new('8', '*'), new('9', '('), new('0', ')'), new('-', '_'), new('=', '+'),
        // Top row: q w e r t y u i o p [ ] \
        new('q', 'Q'), new('w', 'W'), new('e', 'E'), new('r', 'R'), new('t', 'T'), new('y', 'Y'),
        new('u', 'U'), new('i', 'I'), new('o', 'O'), new('p', 'P'), new('[', '{'), new(']', '}'), new('\\', '|'),
        // Home row: a s d f g h j k l ; '
        new('a', 'A'), new('s', 'S'), new('d', 'D'), new('f', 'F'), new('g', 'G'), new('h', 'H'),
        new('j', 'J'), new('k', 'K'), new('l', 'L'), new(';', ':'), new('\'', '"'),
        // Bottom row: z x c v b n m , . /
        new('z', 'Z'), new('x', 'X'), new('c', 'C'), new('v', 'V'), new('b', 'B'), new('n', 'N'),
        new('m', 'M'), new(',', '<'), new('.', '>'), new('/', '?'),
    ];

    // ----- Simplified (two-handed) Dvorak -----
    private static readonly DvorakKey[] DvorakTwoHandsKeys =
    [
        // Number row (digits + symbols identical to QWERTY through '=' except - => [ and = => ])
        new('`', '~'), new('1', '!'), new('2', '@'), new('3', '#'), new('4', '$'), new('5', '%'),
        new('6', '^'), new('7', '&'), new('8', '*'), new('9', '('), new('0', ')'), new('[', '{'), new(']', '}'),
        // Top row
        new('\'', '"'), new(',', '<'), new('.', '>'), new('p', 'P'), new('y', 'Y'), new('f', 'F'),
        new('g', 'G'), new('c', 'C'), new('r', 'R'), new('l', 'L'), new('/', '?'), new('=', '+'), new('\\', '|'),
        // Home row
        new('a', 'A'), new('o', 'O'), new('e', 'E'), new('u', 'U'), new('i', 'I'), new('d', 'D'),
        new('h', 'H'), new('t', 'T'), new('n', 'N'), new('s', 'S'), new('-', '_'),
        // Bottom row
        new(';', ':'), new('q', 'Q'), new('j', 'J'), new('k', 'K'), new('x', 'X'), new('b', 'B'),
        new('m', 'M'), new('w', 'W'), new('v', 'V'), new('z', 'Z'),
    ];

    // ----- One-handed right-hand Dvorak (File:KB_Dvorak_Right.svg) -----
    private static readonly DvorakKey[] DvorakRightHandKeys =
    [
        // Number row
        new('`', '~'), new('1', '!'), new('2', '@'), new('3', '#'), new('4', '$'), new('j', 'J'),
        new('l', 'L'), new('m', 'M'), new('f', 'F'), new('p', 'P'), new('/', '?'), new('[', '{'), new(']', '}'),
        // Top row
        new('5', '%'), new('6', '^'), new('q', 'Q'), new('.', '>'), new('o', 'O'), new('r', 'R'),
        new('s', 'S'), new('u', 'U'), new('y', 'Y'), new('b', 'B'), new(';', ':'), new('=', '+'), new('\\', '|'),
        // Home row
        new('7', '&'), new('8', '*'), new('z', 'Z'), new('a', 'A'), new('e', 'E'), new('h', 'H'),
        new('t', 'T'), new('d', 'D'), new('c', 'C'), new('k', 'K'), new('-', '_'),
        // Bottom row
        new('9', '('), new('0', ')'), new('x', 'X'), new(',', '<'), new('i', 'I'), new('n', 'N'),
        new('w', 'W'), new('v', 'V'), new('g', 'G'), new('\'', '"'),
    ];

    // ----- One-handed left-hand Dvorak (File:KB_Dvorak_Left.svg) -----
    private static readonly DvorakKey[] DvorakLeftHandKeys =
    [
        // Number row
        new('`', '~'), new('[', '{'), new(']', '}'), new('/', '?'), new('p', 'P'), new('f', 'F'),
        new('m', 'M'), new('l', 'L'), new('j', 'J'), new('4', '$'), new('3', '#'), new('2', '@'), new('1', '!'),
        // Top row
        new(';', ':'), new('q', 'Q'), new('b', 'B'), new('y', 'Y'), new('u', 'U'), new('r', 'R'),
        new('s', 'S'), new('o', 'O'), new('.', '>'), new('6', '^'), new('5', '%'), new('=', '+'), new('\\', '|'),
        // Home row
        new('-', '_'), new('k', 'K'), new('c', 'C'), new('d', 'D'), new('t', 'T'), new('h', 'H'),
        new('e', 'E'), new('a', 'A'), new('z', 'Z'), new('8', '*'), new('7', '&'),
        // Bottom row
        new('\'', '"'), new('x', 'X'), new('g', 'G'), new('v', 'V'), new('w', 'W'), new('n', 'N'),
        new('i', 'I'), new(',', '<'), new('0', ')'), new('9', '('),
    ];

    private readonly IReadOnlyDictionary<DvorakLayout, DvorakLayoutInfo> _layouts;

    // Per-layout char -> (index, isShifted) lookup so Remap is O(1) per character.
    private readonly IReadOnlyDictionary<DvorakLayout, IReadOnlyDictionary<char, (int Index, bool Shifted)>> _positions;

    public DvorakCodec()
    {
        var layouts = new Dictionary<DvorakLayout, DvorakLayoutInfo>
        {
            [DvorakLayout.Qwerty] = new(DvorakLayout.Qwerty, QwertyKeys),
            [DvorakLayout.DvorakTwoHands] = new(DvorakLayout.DvorakTwoHands, DvorakTwoHandsKeys),
            [DvorakLayout.DvorakRightHand] = new(DvorakLayout.DvorakRightHand, DvorakRightHandKeys),
            [DvorakLayout.DvorakLeftHand] = new(DvorakLayout.DvorakLeftHand, DvorakLeftHandKeys),
        };

        var positions = new Dictionary<DvorakLayout, IReadOnlyDictionary<char, (int, bool)>>();
        foreach (var (layout, info) in layouts)
        {
            var map = new Dictionary<char, (int, bool)>();
            for (var i = 0; i < info.KeyMap.Count; i++)
            {
                var key = info.KeyMap[i];
                map.TryAdd(key.Unshifted, (i, false));
                map.TryAdd(key.Shifted, (i, true));
            }

            positions[layout] = map;
        }

        _layouts = layouts;
        _positions = positions;
    }

    /// <summary>All layouts in display order.</summary>
    public static IReadOnlyList<DvorakLayout> Layouts { get; } =
    [
        DvorakLayout.Qwerty,
        DvorakLayout.DvorakTwoHands,
        DvorakLayout.DvorakRightHand,
        DvorakLayout.DvorakLeftHand,
    ];

    public DvorakLayoutInfo GetLayout(DvorakLayout layout) => _layouts[layout];

    /// <summary>
    /// Remaps <paramref name="text"/> positionally from <paramref name="from"/> to <paramref name="to"/>.
    /// Each character is located at its physical key on the source layout and re-emitted at that same
    /// key on the target layout (preserving shifted vs. unshifted). Characters with no source key pass
    /// through unchanged. When <paramref name="from"/> equals <paramref name="to"/> the text is returned verbatim.
    /// </summary>
    public string Remap(string? text, DvorakLayout from, DvorakLayout to)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (from == to)
        {
            return text;
        }

        var source = _positions[from];
        var target = _layouts[to].KeyMap;
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (source.TryGetValue(c, out var position))
            {
                var key = target[position.Index];
                builder.Append(position.Shifted ? key.Shifted : key.Unshifted);
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
