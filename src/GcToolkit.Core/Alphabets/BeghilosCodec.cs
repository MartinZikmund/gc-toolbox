using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The substitution table a <see cref="BeghilosCodec"/> uses. The strict profile is the canonical
/// CacheSleuth set (<c>BEGHILOS</c> plus <c>Z</c>); the extended profile additionally decodes the
/// digit <c>9</c> as <c>G</c> for puzzles that use the looser "9 looks like g" convention.
/// </summary>
public enum BeghilosProfile
{
    /// <summary>The canonical readable set: letters <c>BEGHILOSZ</c> ↔ digits <c>836417052</c>.</summary>
    Strict,

    /// <summary>Strict plus the digit <c>9</c> decoding to <c>G</c> (the loose "9 ≈ g" convention).</summary>
    Extended,
}

/// <summary>Which way the calculator-spelling conversion runs.</summary>
public enum BeghilosDirection
{
    /// <summary>Letters → the number to punch into a calculator (substitute then reverse).</summary>
    Encode,

    /// <summary>A number → the word it spells upside-down (reverse then substitute).</summary>
    Decode,
}

/// <summary>The outcome of a <see cref="BeghilosCodec"/> conversion.</summary>
/// <param name="Result">The converted string (digits for an encode, letters for a decode).</param>
/// <param name="UnsupportedCount">How many input characters had no calculator equivalent and were dropped.</param>
/// <param name="UnsupportedCharacters">The distinct unsupported characters, in first-seen order, for surfacing to the user.</param>
public readonly record struct BeghilosResult(
    string Result,
    int UnsupportedCount,
    IReadOnlyList<char> UnsupportedCharacters)
{
    /// <summary><see langword="true"/> when at least one input character could not be converted.</summary>
    public bool HasUnsupported => UnsupportedCount > 0;
}

/// <summary>
/// A pure, stateless "calculator spelling" (BEGHILOS) codec — the single source of truth for the
/// upside-down conversion that hides words inside numbers. Because a seven-segment display is read
/// <em>flipped</em>, the substituted string is also <b>reversed</b>: <see cref="Encode"/> maps each
/// letter to the digit that resembles it upside-down and reverses the result; <see cref="Decode"/>
/// reverses the digit run and maps each digit back to its letter (so <c>"hELLO" ↔ "0.7734"</c>).
/// </summary>
/// <remarks>
/// Mapping (strict profile): B=8, E=3, G=6, H=4, I=1, L=7, O=0, S=5, Z=2 — i.e. letters
/// <c>BEGHILOSZ</c> ↔ digits <c>836417052</c>. The reverse table reads each digit as a single
/// canonical letter (0=O, 1=I, 2=Z, 3=E, 4=H, 5=S, 6=G, 7=L, 8=B); the extended profile adds 9=G.
/// Conversion is case-insensitive and unsupported characters are counted and reported, never silently
/// kept — so the caller can flag them. Both directions are lossless across the BEGHILOS letter set.
/// </remarks>
public sealed class BeghilosCodec
{
    // Source of truth: the canonical letter→digit pairs. Decode picks one canonical letter per digit.
    private static readonly (char Letter, char Digit)[] Pairs =
    [
        ('B', '8'), ('E', '3'), ('G', '6'), ('H', '4'), ('I', '1'),
        ('L', '7'), ('O', '0'), ('S', '5'), ('Z', '2'),
    ];

    private static readonly IReadOnlyDictionary<char, char> LetterToDigit;
    private static readonly IReadOnlyDictionary<char, char> DigitToLetter;

    static BeghilosCodec()
    {
        var letterToDigit = new Dictionary<char, char>();
        var digitToLetter = new Dictionary<char, char>();
        foreach (var (letter, digit) in Pairs)
        {
            letterToDigit[letter] = digit;
            digitToLetter[digit] = letter; // each digit maps to exactly one canonical letter
        }

        LetterToDigit = letterToDigit;
        DigitToLetter = digitToLetter;
    }

    /// <summary>The convertible upper-case letters, in canonical order (<c>BEGHILOSZ</c>).</summary>
    public static IReadOnlyList<char> SupportedLetters { get; } = [.. Pairs.Select(p => p.Letter)];

    /// <summary>The canonical letter→digit pairs, for building a reference/legend display.</summary>
    public static IReadOnlyList<(char Letter, char Digit)> Mapping { get; } = Pairs;

    /// <summary>
    /// Encodes a word into the number to punch into a calculator: substitutes each letter for the digit
    /// that resembles it upside-down (case-insensitive), then <b>reverses</b> the digit string so the
    /// flipped display reads the word. Unsupported characters are dropped and reported.
    /// </summary>
    public BeghilosResult Encode(string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return new BeghilosResult(string.Empty, 0, []);
        }

        var builder = new StringBuilder(word.Length);
        var unsupported = new List<char>();

        foreach (var ch in word)
        {
            if (LetterToDigit.TryGetValue(char.ToUpperInvariant(ch), out var digit))
            {
                builder.Append(digit);
            }
            else
            {
                RecordUnsupported(unsupported, ch);
            }
        }

        Reverse(builder);
        return new BeghilosResult(builder.ToString(), unsupported.Count, unsupported);
    }

    /// <summary>
    /// Decodes a number into the word it spells upside-down: strips formatting (anything that is not a
    /// digit), <b>reverses</b> the digit run, then substitutes each digit for its canonical letter. The
    /// <paramref name="profile"/> chooses whether <c>9</c> is supported (decoded as <c>G</c>). Stripped
    /// non-digit characters are reported as unsupported.
    /// </summary>
    public BeghilosResult Decode(string? number, BeghilosProfile profile = BeghilosProfile.Strict)
    {
        if (string.IsNullOrEmpty(number))
        {
            return new BeghilosResult(string.Empty, 0, []);
        }

        var builder = new StringBuilder(number.Length);
        var unsupported = new List<char>();

        // Reverse first, then substitute, so the display is read flipped.
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var ch = number[i];
            if (DigitToLetter.TryGetValue(ch, out var letter))
            {
                builder.Append(letter);
            }
            else if (profile == BeghilosProfile.Extended && ch == '9')
            {
                builder.Append('G'); // loose "9 ≈ g" convention
            }
            else
            {
                RecordUnsupported(unsupported, ch);
            }
        }

        // Report unsupported characters in their original left-to-right order, not reversed.
        unsupported.Reverse();
        return new BeghilosResult(builder.ToString(), unsupported.Count, unsupported);
    }

    /// <summary>
    /// Runs the conversion in the requested <paramref name="direction"/>, or auto-detects it from the
    /// input when <paramref name="direction"/> is <see langword="null"/> (see <see cref="DetectDirection"/>).
    /// </summary>
    public BeghilosResult Convert(string? input, BeghilosDirection? direction = null, BeghilosProfile profile = BeghilosProfile.Strict)
    {
        var resolved = direction ?? DetectDirection(input);
        return resolved == BeghilosDirection.Encode ? Encode(input) : Decode(input, profile);
    }

    /// <summary>
    /// Guesses the conversion direction from the input: a value that is mostly digits is a number to
    /// <see cref="BeghilosDirection.Decode"/>; otherwise it is a word to <see cref="BeghilosDirection.Encode"/>.
    /// Empty or letter-free-and-digit-free input defaults to <see cref="BeghilosDirection.Encode"/>.
    /// </summary>
    public static BeghilosDirection DetectDirection(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return BeghilosDirection.Encode;
        }

        var digits = 0;
        var letters = 0;
        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
            else if (char.IsLetter(ch))
            {
                letters++;
            }
        }

        // Any letter present means it's a word to encode; only when it's all-digits (or symbols) do we decode.
        return letters == 0 && digits > 0 ? BeghilosDirection.Decode : BeghilosDirection.Encode;
    }

    private static void RecordUnsupported(List<char> unsupported, char ch)
    {
        // Distinct, first-seen order; whitespace is ignored (callers commonly include spaces/dots).
        if (!char.IsWhiteSpace(ch) && !unsupported.Contains(ch))
        {
            unsupported.Add(ch);
        }
    }

    private static void Reverse(StringBuilder builder)
    {
        for (int i = 0, j = builder.Length - 1; i < j; i++, j--)
        {
            (builder[i], builder[j]) = (builder[j], builder[i]);
        }
    }
}
