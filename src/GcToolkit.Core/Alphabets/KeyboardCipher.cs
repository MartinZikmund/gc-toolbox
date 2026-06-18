namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The keyboard layouts whose physical key order defines the substitution alphabet. The first five
/// are fixed, built-in layouts; <see cref="Custom"/> reads its key order from a caller-supplied string.
/// </summary>
public enum KeyboardLayout
{
    /// <summary>US English <c>QWERTY</c> (<c>QWERTYUIOP ASDFGHJKL ZXCVBNM</c>).</summary>
    Qwerty,

    /// <summary>French <c>AZERTY</c> (<c>AZERTYUIOP QSDFGHJKLM WXCVBN</c>).</summary>
    Azerty,

    /// <summary>German <c>QWERTZ</c> (<c>QWERTZUIOP ASDFGHJKL YXCVBNM</c>).</summary>
    Qwertz,

    /// <summary>Simplified Dvorak (<c>PYFGCRL AOEUIDHTNS QJKXBMWVZ</c>).</summary>
    Dvorak,

    /// <summary>Colemak (<c>QWFPGJLUY ARSTDHNEIO ZXCVBKM</c>).</summary>
    Colemak,

    /// <summary>A user-supplied key order (validated as a permutation of the 26 letters).</summary>
    Custom,
}

/// <summary>The direction the cipher runs in.</summary>
public enum KeyboardCipherDirection
{
    /// <summary>Plaintext letter -> keyboard-position letter (<c>A -> Q</c> for QWERTY).</summary>
    Encrypt,

    /// <summary>Keyboard-position letter -> plaintext letter (<c>Q -> A</c> for QWERTY).</summary>
    Decrypt,
}

/// <summary>One <c>A-Z</c> &lt;-&gt; layout-letter pair for the reference table.</summary>
/// <param name="Letter">The plain alphabet letter (<c>A</c>..<c>Z</c>).</param>
/// <param name="Mapped">The layout letter it maps to when encrypting.</param>
public readonly record struct KeyboardMappingEntry(char Letter, char Mapped);

/// <summary>One auto-detect candidate: the <see cref="Layout"/> and <see cref="Direction"/> tried and the resulting <see cref="Text"/>.</summary>
public readonly record struct KeyboardCipherCandidate(KeyboardLayout Layout, KeyboardCipherDirection Direction, string Text);

/// <summary>
/// A pure, stateless keyboard cipher — the single source of truth for the transform. It maps the 26
/// letters A-Z positionally onto a keyboard layout's letter order, read row-by-row, left-to-right,
/// top-to-bottom. For QWERTY the order is <c>QWERTYUIOPASDFGHJKLZXCVBNM</c>, so encrypting maps
/// <c>A -&gt; Q</c>, <c>B -&gt; W</c>, … and decrypting inverts it. Case is preserved and every
/// non-letter passes through unchanged. Beyond cachesleuth.com parity (QWERTY/AZERTY/QWERTZ) it adds
/// Dvorak, Colemak and a validated <see cref="KeyboardLayout.Custom"/> layout, plus an
/// <see cref="AutoDetect"/> brute-force over every layout × direction.
/// </summary>
public sealed class KeyboardCipher
{
    /// <summary>Number of letters in the alphabet (A-Z).</summary>
    public const int AlphabetSize = 26;

    /// <summary>The plain alphabet in order: <c>ABCDEFGHIJKLMNOPQRSTUVWXYZ</c>.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // The letter order of each built-in layout, read row-by-row, left-to-right, top-to-bottom.
    private const string QwertyOrder = "QWERTYUIOPASDFGHJKLZXCVBNM";
    private const string AzertyOrder = "AZERTYUIOPQSDFGHJKLMWXCVBN";
    private const string QwertzOrder = "QWERTZUIOPASDFGHJKLYXCVBNM";
    private const string DvorakOrder = "PYFGCRLAOEUIDHTNSQJKXBMWVZ";
    private const string ColemakOrder = "QWFPGJLUYARSTDHNEIOZXCVBKM";

    /// <summary>The built-in layouts, in display order (excludes <see cref="KeyboardLayout.Custom"/>).</summary>
    public static IReadOnlyList<KeyboardLayout> BuiltInLayouts { get; } =
    [
        KeyboardLayout.Qwerty,
        KeyboardLayout.Azerty,
        KeyboardLayout.Qwertz,
        KeyboardLayout.Dvorak,
        KeyboardLayout.Colemak,
    ];

    /// <summary>
    /// The 26-letter key order for a built-in <paramref name="layout"/>, upper-case. Throws for
    /// <see cref="KeyboardLayout.Custom"/> (its order comes from the caller — use the
    /// <c>customLayout</c> overloads instead).
    /// </summary>
    public static string GetLayoutOrder(KeyboardLayout layout) => layout switch
    {
        KeyboardLayout.Qwerty => QwertyOrder,
        KeyboardLayout.Azerty => AzertyOrder,
        KeyboardLayout.Qwertz => QwertzOrder,
        KeyboardLayout.Dvorak => DvorakOrder,
        KeyboardLayout.Colemak => ColemakOrder,
        _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Custom layouts have no built-in order."),
    };

    /// <summary>
    /// <see langword="true"/> when <paramref name="layout"/> is exactly a permutation of the 26
    /// letters A-Z (case-insensitive, each letter present exactly once). Empty/null is invalid.
    /// </summary>
    public static bool IsValidLayout(string? layout)
    {
        if (string.IsNullOrEmpty(layout) || layout.Length != AlphabetSize)
        {
            return false;
        }

        var seen = 0;
        foreach (var ch in layout)
        {
            var upper = char.ToUpperInvariant(ch);
            if (upper is < 'A' or > 'Z')
            {
                return false;
            }

            var bit = 1 << (upper - 'A');
            if ((seen & bit) != 0)
            {
                return false;
            }

            seen |= bit;
        }

        return true;
    }

    /// <summary>Transforms <paramref name="text"/> with a built-in <paramref name="layout"/> in the given <paramref name="direction"/>.</summary>
    /// <returns><see cref="string.Empty"/> for null/empty input; otherwise the transformed text.</returns>
    public string Transform(string? text, KeyboardLayout layout, KeyboardCipherDirection direction)
        => Transform(text, GetLayoutOrder(layout), direction);

    /// <summary>Encrypts <paramref name="text"/> with a built-in <paramref name="layout"/> (<c>A -&gt; layout[0]</c>).</summary>
    public string Encrypt(string? text, KeyboardLayout layout)
        => Transform(text, layout, KeyboardCipherDirection.Encrypt);

    /// <summary>Decrypts <paramref name="text"/> with a built-in <paramref name="layout"/> (the inverse of <see cref="Encrypt(string?, KeyboardLayout)"/>).</summary>
    public string Decrypt(string? text, KeyboardLayout layout)
        => Transform(text, layout, KeyboardCipherDirection.Decrypt);

    /// <summary>
    /// Transforms <paramref name="text"/> against an arbitrary <paramref name="layoutOrder"/> key
    /// string (the custom-layout path). Throws <see cref="ArgumentException"/> when the order is not
    /// a permutation of the 26 letters.
    /// </summary>
    public string Transform(string? text, string layoutOrder, KeyboardCipherDirection direction)
    {
        if (!IsValidLayout(layoutOrder))
        {
            throw new ArgumentException("Layout must be a permutation of the 26 letters A-Z.", nameof(layoutOrder));
        }

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var order = layoutOrder.ToUpperInvariant();
        // Forward maps alphabet index -> layout letter; inverse maps layout letter -> alphabet index.
        Span<int> inverse = stackalloc int[AlphabetSize];
        for (var i = 0; i < AlphabetSize; i++)
        {
            inverse[order[i] - 'A'] = i;
        }

        var buffer = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            buffer[i] = MapChar(text[i], order, inverse, direction);
        }

        return new string(buffer);
    }

    /// <summary>The full A-Z &lt;-&gt; layout mapping for a built-in <paramref name="layout"/>, for the reference table.</summary>
    public IReadOnlyList<KeyboardMappingEntry> GetMapping(KeyboardLayout layout)
        => GetMapping(GetLayoutOrder(layout));

    /// <summary>The full A-Z &lt;-&gt; layout mapping for an arbitrary <paramref name="layoutOrder"/>.</summary>
    public IReadOnlyList<KeyboardMappingEntry> GetMapping(string layoutOrder)
    {
        if (!IsValidLayout(layoutOrder))
        {
            throw new ArgumentException("Layout must be a permutation of the 26 letters A-Z.", nameof(layoutOrder));
        }

        var order = layoutOrder.ToUpperInvariant();
        var entries = new KeyboardMappingEntry[AlphabetSize];
        for (var i = 0; i < AlphabetSize; i++)
        {
            entries[i] = new KeyboardMappingEntry((char)('A' + i), order[i]);
        }

        return entries;
    }

    /// <summary>
    /// Runs <paramref name="text"/> through every built-in layout in both directions and returns the
    /// candidates (10 in total) so a solver can pick the readable one without guessing the layout.
    /// Dictionary/coordinate scoring is intentionally out of scope here (see the tool's gaps).
    /// </summary>
    public IReadOnlyList<KeyboardCipherCandidate> AutoDetect(string? text)
    {
        var candidates = new List<KeyboardCipherCandidate>(BuiltInLayouts.Count * 2);
        foreach (var layout in BuiltInLayouts)
        {
            candidates.Add(new KeyboardCipherCandidate(layout, KeyboardCipherDirection.Encrypt, Encrypt(text, layout)));
            candidates.Add(new KeyboardCipherCandidate(layout, KeyboardCipherDirection.Decrypt, Decrypt(text, layout)));
        }

        return candidates;
    }

    private static char MapChar(char c, string order, ReadOnlySpan<int> inverse, KeyboardCipherDirection direction)
    {
        if (c is >= 'A' and <= 'Z')
        {
            return direction == KeyboardCipherDirection.Encrypt
                ? order[c - 'A']
                : (char)('A' + inverse[c - 'A']);
        }

        if (c is >= 'a' and <= 'z')
        {
            return direction == KeyboardCipherDirection.Encrypt
                ? char.ToLowerInvariant(order[c - 'a'])
                : (char)('a' + inverse[c - 'a']);
        }

        return c;
    }
}
