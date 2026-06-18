using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>The Base32 alphabet a <see cref="Base32Codec"/> encodes to / decodes from.</summary>
public enum Base32Variant
{
    /// <summary>RFC 4648 §6: <c>A–Z 2–7</c>, <c>=</c> padding. The canonical Base32 used in most puzzles.</summary>
    Rfc4648,

    /// <summary>RFC 4648 §7 "base32hex": extended hex alphabet <c>0–9 A–V</c>, sorts in the same order as the bytes.</summary>
    Base32Hex,

    /// <summary>Crockford Base32: <c>0–9 A–Z</c> excluding <c>I L O U</c>. Decode is lenient (I/L→1, O→0, hyphens ignored).</summary>
    Crockford,

    /// <summary>z-base-32 (<c>ybndrfg8ejkmcpqxot1uwisza345h769</c>): a human-oriented, unpadded permutation.</summary>
    ZBase32,
}

/// <summary>Where a lenient decode first encountered an out-of-alphabet character.</summary>
/// <param name="InvalidChar">The offending character.</param>
/// <param name="Position">Its zero-based index in the <em>original</em> input (separators included).</param>
public readonly record struct Base32DecodeError(char InvalidChar, int Position);

/// <summary>One variant's decode attempt, used by <see cref="Base32Codec.TryAllVariants"/>.</summary>
/// <param name="Variant">The variant tried.</param>
/// <param name="Success">Whether the input decoded cleanly under <see cref="Variant"/>.</param>
/// <param name="Bytes">The decoded bytes (empty on failure).</param>
/// <param name="Error">The first invalid character on failure, otherwise <see langword="null"/>.</param>
public readonly record struct Base32VariantResult(Base32Variant Variant, bool Success, byte[] Bytes, Base32DecodeError? Error);

/// <summary>
/// A pure, head-independent Base32 codec covering the common geocaching variants — RFC 4648,
/// base32hex, Crockford and z-base-32 — plus arbitrary custom alphabets. Encoding turns each group
/// of 5 input bytes into 8 symbols (with optional <c>=</c> padding); decoding is deliberately lenient:
/// case-insensitive, separators (spaces, newlines, hyphens, punctuation) ignored, padding optional,
/// Crockford look-alikes normalized, and the <em>first</em> illegal character reported via
/// <see cref="Base32DecodeError"/> rather than thrown. Goes beyond cachesleuth.com with custom
/// alphabets, <see cref="AutoDetect"/> and <see cref="TryAllVariants"/> brute forcing.
/// </summary>
public sealed class Base32Codec
{
    private const int BitsPerSymbol = 5;
    private const char Pad = '=';

    private const string Rfc4648Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string Base32HexAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const string ZBase32Alphabet = "ybndrfg8ejkmcpqxot1uwisza345h769";

    /// <summary>All variants in display order — drives <see cref="TryAllVariants"/> and the picker UI.</summary>
    public static IReadOnlyList<Base32Variant> AllVariants { get; } =
        [Base32Variant.Rfc4648, Base32Variant.Base32Hex, Base32Variant.Crockford, Base32Variant.ZBase32];

    /// <summary>The 32-symbol alphabet for <paramref name="variant"/> (upper-case where applicable).</summary>
    public static string AlphabetFor(Base32Variant variant) => variant switch
    {
        Base32Variant.Rfc4648 => Rfc4648Alphabet,
        Base32Variant.Base32Hex => Base32HexAlphabet,
        Base32Variant.Crockford => CrockfordAlphabet,
        Base32Variant.ZBase32 => ZBase32Alphabet,
        _ => Rfc4648Alphabet,
    };

    /// <summary>Whether a variant pads with <c>=</c> by default (RFC variants do; Crockford / z-base-32 don't).</summary>
    public static bool DefaultPadding(Base32Variant variant)
        => variant is Base32Variant.Rfc4648 or Base32Variant.Base32Hex;

    /// <summary>
    /// Encodes <paramref name="data"/> using <paramref name="variant"/>'s alphabet. Pass
    /// <paramref name="padding"/> explicitly to force <c>=</c> padding on/off; leave it <see langword="null"/>
    /// to follow the variant's convention (<see cref="DefaultPadding"/>).
    /// </summary>
    public string Encode(byte[] data, Base32Variant variant, bool? padding = null)
        => EncodeCore(data, AlphabetFor(variant), padding ?? DefaultPadding(variant));

    /// <summary>Encodes <paramref name="data"/> with a caller-supplied 32-symbol <paramref name="alphabet"/>.</summary>
    /// <exception cref="ArgumentException">The alphabet is not exactly 32 distinct symbols.</exception>
    public string EncodeCustom(byte[] data, string alphabet, bool padding = false)
    {
        if (!IsValidCustomAlphabet(alphabet))
        {
            throw new ArgumentException("Alphabet must contain exactly 32 distinct symbols.", nameof(alphabet));
        }

        return EncodeCore(data, alphabet, padding);
    }

    /// <summary>
    /// Leniently decodes <paramref name="text"/> under <paramref name="variant"/>. Returns
    /// <see langword="true"/> on success with <paramref name="bytes"/> set; on failure returns
    /// <see langword="false"/> and sets <paramref name="error"/> to the first illegal character (and its
    /// original-string position). Empty/separator-only input is a success with zero bytes.
    /// </summary>
    /// <param name="usedSymbolCount">How many alphabet symbols were consumed (separators excluded) — useful for UI stats.</param>
    public bool TryDecode(string? text, Base32Variant variant, out byte[] bytes, out Base32DecodeError? error, out int usedSymbolCount)
        => TryDecodeCore(text, BuildLookup(variant), out bytes, out error, out usedSymbolCount);

    /// <summary>Leniently decodes <paramref name="text"/> with a caller-supplied <paramref name="alphabet"/>.</summary>
    public bool TryDecodeCustom(string? text, string alphabet, out byte[] bytes, out Base32DecodeError? error)
    {
        if (!IsValidCustomAlphabet(alphabet))
        {
            bytes = [];
            error = null;
            return false;
        }

        return TryDecodeCore(text, BuildCustomLookup(alphabet), out bytes, out error, out _);
    }

    /// <summary>
    /// Guesses the most likely variant from <paramref name="text"/>'s charset: characters unique to one
    /// alphabet narrow the choice; ties fall back to <see cref="Base32Variant.Rfc4648"/>. Always returns a
    /// variant (it never fails) — pair with <see cref="TryAllVariants"/> to brute-force when unsure.
    /// </summary>
    public Base32Variant AutoDetect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Base32Variant.Rfc4648;
        }

        // Score each variant by how many of the input's significant chars it can represent.
        var best = Base32Variant.Rfc4648;
        var bestScore = -1;
        foreach (var variant in AllVariants)
        {
            var lookup = BuildLookup(variant);
            var score = 0;
            var fits = true;
            foreach (var c in text)
            {
                if (IsSeparator(c) || c == Pad)
                {
                    continue;
                }

                if (lookup.TryGetValue(c, out _))
                {
                    score++;
                }
                else
                {
                    fits = false;
                    break;
                }
            }

            // Prefer a variant that represents every char; among those, the most specific wins by order.
            if (fits && score > bestScore)
            {
                bestScore = score;
                best = variant;
            }
        }

        return best;
    }

    /// <summary>Decodes <paramref name="text"/> under every built-in variant — the brute-force "solve" mode.</summary>
    public IReadOnlyList<Base32VariantResult> TryAllVariants(string? text)
    {
        var results = new List<Base32VariantResult>(AllVariants.Count);
        foreach (var variant in AllVariants)
        {
            var success = TryDecode(text, variant, out var bytes, out var error, out _);
            results.Add(new Base32VariantResult(variant, success, bytes, error));
        }

        return results;
    }

    /// <summary>True when <paramref name="alphabet"/> is exactly 32 distinct characters (custom-alphabet rule).</summary>
    public static bool IsValidCustomAlphabet(string? alphabet)
        => alphabet is { Length: 32 } && alphabet.Distinct().Count() == 32;

    private static string EncodeCore(byte[] data, string alphabet, bool padding)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((data.Length + 4) / 5 * 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= BitsPerSymbol)
            {
                bitsLeft -= BitsPerSymbol;
                builder.Append(alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        // Flush the final partial group, left-aligned.
        if (bitsLeft > 0)
        {
            builder.Append(alphabet[(buffer << (BitsPerSymbol - bitsLeft)) & 0x1F]);
        }

        if (padding)
        {
            while (builder.Length % 8 != 0)
            {
                builder.Append(Pad);
            }
        }

        return builder.ToString();
    }

    private static bool TryDecodeCore(string? text, Dictionary<char, int> lookup, out byte[] bytes, out Base32DecodeError? error, out int usedSymbolCount)
    {
        bytes = [];
        error = null;
        usedSymbolCount = 0;

        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var buffer = 0;
        var bitsLeft = 0;
        var output = new List<byte>(text.Length * BitsPerSymbol / 8 + 1);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsSeparator(c) || c == Pad)
            {
                continue;
            }

            if (!lookup.TryGetValue(c, out var symbol))
            {
                error = new Base32DecodeError(c, i);
                bytes = [];
                return false;
            }

            usedSymbolCount++;
            buffer = (buffer << BitsPerSymbol) | symbol;
            bitsLeft += BitsPerSymbol;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        // Trailing bits below a full byte are padding remnants and are dropped (lenient).
        bytes = [.. output];
        return true;
    }

    /// <summary>Builds the decode lookup for a variant, folding in case-insensitivity and Crockford look-alikes.</summary>
    private static Dictionary<char, int> BuildLookup(Base32Variant variant)
    {
        var lookup = BuildCustomLookup(AlphabetFor(variant));

        if (variant == Base32Variant.Crockford)
        {
            // Crockford decode accepts ambiguous look-alikes for the digits they resemble.
            AddAlias(lookup, 'O', '0');
            AddAlias(lookup, 'I', '1');
            AddAlias(lookup, 'L', '1');
        }

        return lookup;
    }

    /// <summary>Case-insensitive symbol→index map for an arbitrary alphabet (lower-case fills only where free).</summary>
    private static Dictionary<char, int> BuildCustomLookup(string alphabet)
    {
        var lookup = new Dictionary<char, int>(alphabet.Length * 2);
        for (var i = 0; i < alphabet.Length; i++)
        {
            var c = alphabet[i];
            lookup[c] = i;

            var upper = char.ToUpperInvariant(c);
            var lower = char.ToLowerInvariant(c);
            lookup.TryAdd(upper, i);
            lookup.TryAdd(lower, i);
        }

        return lookup;
    }

    private static void AddAlias(Dictionary<char, int> lookup, char alias, char canonical)
    {
        if (lookup.TryGetValue(canonical, out var index))
        {
            lookup[char.ToUpperInvariant(alias)] = index;
            lookup[char.ToLowerInvariant(alias)] = index;
        }
    }

    /// <summary>Characters ignored during a lenient decode (whitespace, hyphens, and common punctuation).</summary>
    private static bool IsSeparator(char c)
        => char.IsWhiteSpace(c) || c is '-' or '_' or '.' or ',' or ':' or ';' or '|' or '/' or '\\';
}
