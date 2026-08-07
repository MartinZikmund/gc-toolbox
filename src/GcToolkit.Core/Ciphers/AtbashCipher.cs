namespace GcToolkit.Core.Ciphers;

/// <summary>
/// A pure, stateless Atbash cipher — the single source of truth for the transform. Maps each Latin
/// letter to its mirror in the alphabet (A↔Z, B↔Y, …, so the 0-based index <c>i</c> becomes
/// <c>25 − i</c>), preserves letter case, and passes every non-letter (digits, punctuation, whitespace,
/// and all non-Latin/Unicode characters) through unchanged. Atbash is its own inverse, so a single
/// <see cref="Transform"/> both encodes and decodes — there is no direction toggle. Beyond
/// geocachingtoolbox.com parity it preserves case, passes full Unicode through, and exposes the fixed
/// substitution key (<see cref="PlainAlphabet"/> / <see cref="CipherAlphabet"/>) for a "key" display.
/// </summary>
public sealed class AtbashCipher
{
    /// <summary>The ordered plain alphabet, <c>A…Z</c> — the top row of the substitution "key".</summary>
    public const string PlainAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>The Atbash substitution row, <c>Z…A</c> — aligned beneath <see cref="PlainAlphabet"/>.</summary>
    public const string CipherAlphabet = "ZYXWVUTSRQPONMLKJIHGFEDCBA";

    /// <summary>
    /// Mirrors every Latin letter of <paramref name="text"/> (A↔Z, B↔Y, …), keeping each letter's case
    /// and emitting every other character unchanged. Because Atbash is self-inverse, applying this twice
    /// returns the original text.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the mirrored text.</returns>
    public string Transform(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Mirror(source[i]);
            }
        });
    }

    private static char Mirror(char c) => c switch
    {
        >= 'A' and <= 'Z' => (char)('Z' - (c - 'A')),
        >= 'a' and <= 'z' => (char)('z' - (c - 'a')),
        _ => c,
    };
}
