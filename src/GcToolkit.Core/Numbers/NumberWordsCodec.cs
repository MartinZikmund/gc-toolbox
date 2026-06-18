using System.Globalization;
using System.Numerics;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>How a number is spelled out.</summary>
public enum NumberWordsMode
{
    /// <summary>Standard counting words (ONE, TWENTY-ONE, ONE HUNDRED).</summary>
    Cardinal,

    /// <summary>Positional words (FIRST, TWENTY-THIRD, ONE HUNDREDTH).</summary>
    Ordinal,

    /// <summary>Year reading: paired tens (1984 → NINETEEN EIGHTY-FOUR).</summary>
    Year,
}

/// <summary>Spelling language for the words.</summary>
public enum NumberWordsLanguage
{
    English,
    Czech,
}

/// <summary>Direction a <see cref="NumberWordsCodec.Convert"/> call resolved to.</summary>
public enum NumberWordsDirection
{
    NumberToWords,
    WordsToNumber,
}

/// <summary>
/// Options controlling how <see cref="NumberWordsCodec"/> spells a number. Immutable; use
/// <c>with</c> to vary a single facet (e.g. <c>NumberWordsOptions.Default with { UseAnd = true }</c>).
/// </summary>
/// <param name="Mode">Cardinal, ordinal, or year reading.</param>
/// <param name="Language">English or Czech spelling.</param>
/// <param name="UseAnd">British-style "and" before the final sub-hundred group (ONE HUNDRED AND ONE). English only.</param>
public readonly record struct NumberWordsOptions(
    NumberWordsMode Mode = NumberWordsMode.Cardinal,
    NumberWordsLanguage Language = NumberWordsLanguage.English,
    bool UseAnd = false)
{
    /// <summary>American cardinal English — the default conversion.</summary>
    public static NumberWordsOptions Default => new();
}

/// <summary>The outcome of converting one value (either direction).</summary>
/// <param name="Text">The produced words or digits; empty when invalid.</param>
/// <param name="IsValid"><see langword="true"/> when the input converted cleanly.</param>
/// <param name="Direction">Which way the conversion went.</param>
/// <param name="Source">The original input token/line, echoed for batch displays.</param>
public readonly record struct NumberWordsResult(string Text, bool IsValid, NumberWordsDirection Direction, string Source);

/// <summary>
/// Bidirectional number ↔ words translator. Encoding spells whole numbers in the English short scale
/// up to a nonillion (10^30) — and beyond with <see cref="EncodeBig"/> — and supports negatives,
/// decimals, ordinals, year reading, a British "and" style and a Czech spelling mode. Decoding is
/// lenient: hyphens, commas and the word "and" are ignored, so <c>three hundred and fifty-four</c> and
/// <c>354</c> both parse. <see cref="Convert"/> auto-detects the direction from the input.
/// </summary>
public sealed class NumberWordsCodec
{
    // 0–19 read directly; tens index by (n/10). Empty slots are never indexed.
    private static readonly string[] EnOnes =
    [
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
        "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN",
        "SEVENTEEN", "EIGHTEEN", "NINETEEN",
    ];

    private static readonly string[] EnTens =
    [
        "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY",
    ];

    /// <summary>Short-scale group names, index = group position (1 = thousand … 10 = nonillion).</summary>
    private static readonly string[] EnScales =
    [
        "", "THOUSAND", "MILLION", "BILLION", "TRILLION", "QUADRILLION", "QUINTILLION",
        "SEXTILLION", "SEPTILLION", "OCTILLION", "NONILLION",
    ];

    // Irregular cardinal→ordinal word swaps; everything else takes a "TH" suffix.
    private static readonly Dictionary<string, string> EnOrdinalOnes = new(StringComparer.Ordinal)
    {
        ["ONE"] = "FIRST",
        ["TWO"] = "SECOND",
        ["THREE"] = "THIRD",
        ["FIVE"] = "FIFTH",
        ["EIGHT"] = "EIGHTH",
        ["NINE"] = "NINTH",
        ["TWELVE"] = "TWELFTH",
    };

    private static readonly Dictionary<string, string> EnOrdinalTens = new(StringComparer.Ordinal)
    {
        ["TWENTY"] = "TWENTIETH",
        ["THIRTY"] = "THIRTIETH",
        ["FORTY"] = "FORTIETH",
        ["FIFTY"] = "FIFTIETH",
        ["SIXTY"] = "SIXTIETH",
        ["SEVENTY"] = "SEVENTIETH",
        ["EIGHTY"] = "EIGHTIETH",
        ["NINETY"] = "NINETIETH",
    };

    // ---- Decode lookup (English) ----

    private static readonly Dictionary<string, long> EnSmallWords = BuildEnSmallWords();
    private static readonly Dictionary<string, BigInteger> EnMultipliers = BuildEnMultipliers();
    private static readonly Dictionary<string, string> EnOrdinalToCardinal = BuildEnOrdinalToCardinal();

    // ---- Czech ----

    private static readonly string[] CzOnes =
    [
        "NULA", "JEDNA", "DVA", "TŘI", "ČTYŘI", "PĚT", "ŠEST", "SEDM", "OSM", "DEVĚT",
        "DESET", "JEDENÁCT", "DVANÁCT", "TŘINÁCT", "ČTRNÁCT", "PATNÁCT", "ŠESTNÁCT",
        "SEDMNÁCT", "OSMNÁCT", "DEVATENÁCT",
    ];

    private static readonly string[] CzTens =
    [
        "", "", "DVACET", "TŘICET", "ČTYŘICET", "PADESÁT", "ŠEDESÁT", "SEDMDESÁT", "OSMDESÁT", "DEVADESÁT",
    ];

    // Hundreds are irregular in Czech: STO / DVĚ STĚ / TŘI STA …
    private static readonly string[] CzHundreds =
    [
        "", "STO", "DVĚ STĚ", "TŘI STA", "ČTYŘI STA", "PĚT SET", "ŠEST SET", "SEDM SET", "OSM SET", "DEVĚT SET",
    ];

    // Czech scale words: [singular, paucal (2–4), plural/genitive (0,5+)].
    private static readonly (string One, string Few, string Many)[] CzScales =
    [
        ("", "", ""),
        ("TISÍC", "TISÍCE", "TISÍC"),
        ("MILION", "MILIONY", "MILIONŮ"),
        ("MILIARDA", "MILIARDY", "MILIARD"),
        ("BILION", "BILIONY", "BILIONŮ"),
    ];

    /// <summary>Largest short-scale group index this codec names (nonillion = group 10, i.e. up to 10^33-1).</summary>
    public const int MaxScaleGroup = 10;

    // ===================== Encode (long) =====================

    /// <summary>Spells <paramref name="value"/> using the default (American cardinal English) options.</summary>
    public string Encode(long value) => Encode(value, NumberWordsOptions.Default);

    /// <summary>Spells <paramref name="value"/> according to <paramref name="options"/>.</summary>
    public string Encode(long value, NumberWordsOptions options) => options.Language switch
    {
        NumberWordsLanguage.Czech => EncodeCzech(value, options),
        _ => EncodeEnglish(value, options),
    };

    /// <summary>Spells a <see cref="BigInteger"/> in cardinal English, throwing on out-of-range (above nonillion group).</summary>
    public string EncodeBig(BigInteger value)
        => TryEncodeBig(value, NumberWordsOptions.Default, out var words)
            ? words
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Value exceeds the largest named scale (nonillion).");

    /// <summary>Attempts to spell an arbitrarily large integer. Fails only when it exceeds the nonillion scale (English).</summary>
    public bool TryEncodeBig(BigInteger value, NumberWordsOptions options, out string words)
    {
        words = string.Empty;
        var negative = value.Sign < 0;
        var magnitude = negative ? -value : value;

        string body;
        if (options.Language == NumberWordsLanguage.Czech)
        {
            body = SpellBigCzech(magnitude);
        }
        else
        {
            var english = SpellBigEnglish(magnitude, options.UseAnd);
            if (english is null)
            {
                return false;
            }

            body = english;
        }

        words = negative ? $"{NegativeWord(options.Language)} {body}" : body;
        if (options.Mode == NumberWordsMode.Ordinal && options.Language == NumberWordsLanguage.English)
        {
            words = ToOrdinal(words);
        }

        return true;
    }

    private string EncodeEnglish(long value, NumberWordsOptions options)
    {
        if (options.Mode == NumberWordsMode.Year && value is > 0 and < 10000)
        {
            return EncodeYear((int)value);
        }

        var negative = value < 0;
        // Negate via BigInteger so long.MinValue is safe.
        var magnitude = negative ? -(BigInteger)value : value;
        var body = SpellBigEnglish(magnitude, options.UseAnd) ?? EnOnes[0];

        var result = negative ? $"MINUS {body}" : body;
        return options.Mode == NumberWordsMode.Ordinal ? ToOrdinal(result) : result;
    }

    /// <summary>Spells a non-negative magnitude in short-scale English, or <see langword="null"/> if it overflows the named scales.</summary>
    private static string? SpellBigEnglish(BigInteger magnitude, bool useAnd)
    {
        if (magnitude.IsZero)
        {
            return EnOnes[0];
        }

        // Decompose into base-1000 groups, least significant first.
        var groups = new List<int>();
        var remaining = magnitude;
        while (remaining > 0)
        {
            groups.Add((int)(remaining % 1000));
            remaining /= 1000;
        }

        if (groups.Count - 1 > MaxScaleGroup)
        {
            return null;
        }

        var parts = new List<string>();
        var highestGroupIndex = groups.Count - 1;
        for (var g = highestGroupIndex; g >= 0; g--)
        {
            var groupValue = groups[g];
            if (groupValue == 0)
            {
                continue;
            }

            // British "and": precede the trailing sub-hundred remainder with "AND" — both within a
            // group (ONE HUNDRED AND ONE) and across groups (ONE THOUSAND AND ONE).
            var precededByHigher = parts.Count > 0;
            var groupWords = SpellGroupEnglish(groupValue, useAnd, g == 0 && precededByHigher);
            parts.Add(g > 0 ? $"{groupWords} {EnScales[g]}" : groupWords);
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Spells 1–999. With <paramref name="useAnd"/> the trailing 1–99 remainder is preceded by "AND"
    /// when this group also has hundreds, or — via <paramref name="precededByHigher"/> — when a higher
    /// scale group came before it (the cross-group British "AND").
    /// </summary>
    private static string SpellGroupEnglish(int value, bool useAnd, bool precededByHigher)
    {
        var parts = new List<string>();
        var hundreds = value / 100;
        var rest = value % 100;

        if (hundreds > 0)
        {
            parts.Add($"{EnOnes[hundreds]} HUNDRED");
        }

        if (rest > 0)
        {
            if (useAnd && (hundreds > 0 || precededByHigher))
            {
                parts.Add("AND");
            }

            parts.Add(SpellTwoDigitEnglish(rest));
        }

        return string.Join(" ", parts);
    }

    private static string SpellTwoDigitEnglish(int value)
    {
        if (value < 20)
        {
            return EnOnes[value];
        }

        var tens = EnTens[value / 10];
        var ones = value % 10;
        return ones == 0 ? tens : $"{tens}-{EnOnes[ones]}";
    }

    private static string EncodeYear(int year)
    {
        var high = year / 100;
        var low = year % 100;

        // Years that read naturally as a cardinal: round centuries (2000) and the 2000–2009 band.
        if (low == 0 && high % 10 == 0)
        {
            return SpellBigEnglish(year, useAnd: false)!;
        }

        if (high == 0)
        {
            // 1–99: just the number.
            return SpellTwoDigitEnglish(year);
        }

        var highWords = SpellTwoDigitEnglish(high);
        if (low == 0)
        {
            return $"{highWords} HUNDRED";
        }

        // "OH-FIVE" for a single low digit (1805 → EIGHTEEN OH-FIVE).
        var lowWords = low < 10 ? $"OH-{EnOnes[low]}" : SpellTwoDigitEnglish(low);
        return $"{highWords} {lowWords}";
    }

    /// <summary>Turns a finished cardinal phrase into its ordinal by swapping/suffixing the last word.</summary>
    private static string ToOrdinal(string cardinal)
    {
        var space = cardinal.LastIndexOf(' ');
        var prefix = space < 0 ? string.Empty : cardinal[..(space + 1)];
        var last = space < 0 ? cardinal : cardinal[(space + 1)..];

        // The last word may be hyphenated (e.g. TWENTY-FIRST): only its final segment changes.
        var hyphen = last.LastIndexOf('-');
        var head = hyphen < 0 ? string.Empty : last[..(hyphen + 1)];
        var tail = hyphen < 0 ? last : last[(hyphen + 1)..];

        var ordinalTail = OrdinalizeWord(tail);
        return $"{prefix}{head}{ordinalTail}";
    }

    private static string OrdinalizeWord(string word)
    {
        if (EnOrdinalOnes.TryGetValue(word, out var ones))
        {
            return ones;
        }

        if (EnOrdinalTens.TryGetValue(word, out var tens))
        {
            return tens;
        }

        // HUNDRED → HUNDREDTH, THOUSAND → THOUSANDTH, MILLION → MILLIONTH, etc.; regular words add TH.
        return $"{word}TH";
    }

    // ===================== Encode (Czech) =====================

    private static string EncodeCzech(long value, NumberWordsOptions options)
    {
        if (value == 0)
        {
            return CzOnes[0];
        }

        var negative = value < 0;
        var magnitude = negative ? -(BigInteger)value : value;
        var body = SpellBigCzech(magnitude);
        return negative ? $"MÍNUS {body}" : body;
    }

    private static string SpellBigCzech(BigInteger magnitude)
    {
        if (magnitude.IsZero)
        {
            return CzOnes[0];
        }

        var groups = new List<int>();
        var remaining = magnitude;
        while (remaining > 0)
        {
            groups.Add((int)(remaining % 1000));
            remaining /= 1000;
        }

        var parts = new List<string>();
        for (var g = groups.Count - 1; g >= 0; g--)
        {
            var groupValue = groups[g];
            if (groupValue == 0)
            {
                continue;
            }

            if (g == 0)
            {
                parts.Add(SpellGroupCzech(groupValue));
            }
            else if (g < CzScales.Length)
            {
                parts.Add(CzScaleWord(groupValue, g));
            }
            else
            {
                // Beyond our Czech scale table: fall back to the group plus a numeric exponent marker.
                parts.Add($"{SpellGroupCzech(groupValue)} ×10^{g * 3}");
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>Spells the scale portion (e.g. "DVA TISÍCE", "PĚT MILIONŮ") with correct Czech plural form.</summary>
    private static string CzScaleWord(int groupValue, int scaleIndex)
    {
        var (one, few, many) = CzScales[scaleIndex];
        var scale = groupValue switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };

        // "TISÍC" stands alone (no "JEDNA"); "DVA TISÍCE", "PĚT TISÍC" prefix the count.
        if (groupValue == 1)
        {
            return scale;
        }

        return $"{SpellGroupCzech(groupValue)} {scale}";
    }

    private static string SpellGroupCzech(int value)
    {
        var parts = new List<string>();
        var hundreds = value / 100;
        var rest = value % 100;

        if (hundreds > 0)
        {
            parts.Add(CzHundreds[hundreds]);
        }

        if (rest > 0)
        {
            if (rest < 20)
            {
                parts.Add(CzOnes[rest]);
            }
            else
            {
                parts.Add(CzTens[rest / 10]);
                var ones = rest % 10;
                if (ones > 0)
                {
                    parts.Add(CzOnes[ones]);
                }
            }
        }

        return string.Join(" ", parts);
    }

    private static string NegativeWord(NumberWordsLanguage language)
        => language == NumberWordsLanguage.Czech ? "MÍNUS" : "MINUS";

    // ===================== Decimals =====================

    /// <summary>Spells a decimal: integer part as words, then POINT and each fractional digit individually.</summary>
    public string EncodeDecimal(decimal value) => EncodeDecimal(value, NumberWordsOptions.Default);

    /// <summary>Spells a decimal under <paramref name="options"/> (POINT/digit reading for the fraction).</summary>
    public string EncodeDecimal(decimal value, NumberWordsOptions options)
    {
        var negative = value < 0;
        var magnitude = Math.Abs(value);

        // Split on the decimal point of the invariant string form to keep trailing-zero fidelity.
        var text = magnitude.ToString(CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        var integerPart = dot < 0 ? text : text[..dot];
        var fractionPart = dot < 0 ? string.Empty : text[(dot + 1)..];

        var intValue = BigInteger.Parse(integerPart, CultureInfo.InvariantCulture);
        var intWords = SpellBigEnglish(intValue, options.UseAnd) ?? EnOnes[0];

        var builder = new StringBuilder();
        if (negative)
        {
            builder.Append("MINUS ");
        }

        builder.Append(intWords);
        if (fractionPart.Length > 0)
        {
            builder.Append(" POINT");
            foreach (var digit in fractionPart)
            {
                builder.Append(' ').Append(EnOnes[digit - '0']);
            }
        }

        return builder.ToString();
    }

    // ===================== Decode =====================

    /// <summary>
    /// Parses English number words into a number. Lenient: case-insensitive, and hyphens, commas and
    /// the word "and" are ignored. Handles negatives ("minus"/"negative") and ordinals.
    /// </summary>
    public bool TryDecode(string? words, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(words))
        {
            return false;
        }

        var tokens = Tokenize(words);
        if (tokens.Count == 0)
        {
            return false;
        }

        var negative = false;
        if (tokens[0] is "MINUS" or "NEGATIVE")
        {
            negative = true;
            tokens.RemoveAt(0);
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        if (!TryAccumulate(tokens, out var total))
        {
            return false;
        }

        if (total > long.MaxValue)
        {
            return false;
        }

        value = negative ? -(long)total : (long)total;
        return true;
    }

    private static bool TryAccumulate(List<string> tokens, out BigInteger total)
    {
        total = 0;
        BigInteger current = 0;
        var sawAny = false;

        foreach (var raw in tokens)
        {
            var token = NormalizeOrdinalToken(raw);

            if (EnSmallWords.TryGetValue(token, out var small))
            {
                current += small;
                sawAny = true;
            }
            else if (token == "HUNDRED")
            {
                current = (current == 0 ? 1 : current) * 100;
                sawAny = true;
            }
            else if (EnMultipliers.TryGetValue(token, out var multiplier))
            {
                current = (current == 0 ? 1 : current) * multiplier;
                total += current;
                current = 0;
                sawAny = true;
            }
            else
            {
                return false;
            }
        }

        total += current;
        return sawAny;
    }

    /// <summary>Maps an ordinal token back to its cardinal form so "twenty-third" parses as 23.</summary>
    private static string NormalizeOrdinalToken(string token)
        => EnOrdinalToCardinal.TryGetValue(token, out var cardinal) ? cardinal : token;

    /// <summary>Splits on whitespace and hyphens, upper-cases, and drops commas and "and".</summary>
    private static List<string> Tokenize(string words)
    {
        var cleaned = words.Replace(",", " ").Replace("-", " ");
        var parts = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var upper = part.ToUpperInvariant();
            if (upper == "AND")
            {
                continue;
            }

            tokens.Add(upper);
        }

        return tokens;
    }

    // ===================== Auto-detect convert =====================

    /// <summary>
    /// Converts one value, auto-detecting direction: a number (digits, optional sign/decimal/grouping
    /// commas) spells to words; otherwise the input is parsed as words back to a number.
    /// </summary>
    public NumberWordsResult Convert(string? input, NumberWordsOptions options)
    {
        var source = input ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return new NumberWordsResult(string.Empty, false, NumberWordsDirection.NumberToWords, source);
        }

        var trimmed = input.Trim();
        if (LooksNumeric(trimmed))
        {
            return ConvertNumeric(trimmed, options, source);
        }

        if (TryDecode(trimmed, out var number))
        {
            return new NumberWordsResult(
                number.ToString(CultureInfo.InvariantCulture), true, NumberWordsDirection.WordsToNumber, source);
        }

        return new NumberWordsResult(string.Empty, false, NumberWordsDirection.WordsToNumber, source);
    }

    private NumberWordsResult ConvertNumeric(string trimmed, NumberWordsOptions options, string source)
    {
        // Strip grouping commas/spaces but keep sign and a single decimal point.
        var compact = trimmed.Replace(",", string.Empty).Replace(" ", string.Empty);

        if (compact.Contains('.'))
        {
            if (decimal.TryParse(compact, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            {
                return new NumberWordsResult(
                    EncodeDecimal(dec, options), true, NumberWordsDirection.NumberToWords, source);
            }

            return new NumberWordsResult(string.Empty, false, NumberWordsDirection.NumberToWords, source);
        }

        var negative = compact.StartsWith('-');
        var digits = negative || compact.StartsWith('+') ? compact[1..] : compact;
        if (BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var magnitude))
        {
            var signed = negative ? -magnitude : magnitude;

            // Values that fit a long go through Encode, which honors year/ordinal/Czech modes; larger
            // ones use the BigInteger short-scale speller (cardinal/ordinal English only).
            if (signed >= long.MinValue && signed <= long.MaxValue)
            {
                return new NumberWordsResult(Encode((long)signed, options), true, NumberWordsDirection.NumberToWords, source);
            }

            if (TryEncodeBig(signed, options, out var words))
            {
                return new NumberWordsResult(words, true, NumberWordsDirection.NumberToWords, source);
            }
        }

        return new NumberWordsResult(string.Empty, false, NumberWordsDirection.NumberToWords, source);
    }

    private static bool LooksNumeric(string trimmed)
    {
        var any = false;
        foreach (var c in trimmed)
        {
            if (char.IsDigit(c))
            {
                any = true;
            }
            else if (c is not ('-' or '+' or '.' or ',' or ' '))
            {
                return false;
            }
        }

        return any;
    }

    /// <summary>Converts each non-empty line independently (batch mode), echoing the source on each result.</summary>
    public IReadOnlyList<NumberWordsResult> ConvertBatch(string? input, NumberWordsOptions options)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var lines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var results = new List<NumberWordsResult>(lines.Length);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                results.Add(Convert(line, options));
            }
        }

        return results;
    }

    // ===================== Solver helpers =====================

    /// <summary>Letter count of each whitespace/hyphen-separated word (non-letters ignored).</summary>
    public static IReadOnlyList<int> LetterCountsPerWord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var words = SplitWords(text);
        var counts = new List<int>(words.Length);
        foreach (var word in words)
        {
            counts.Add(word.Count(char.IsLetter));
        }

        return counts;
    }

    /// <summary>Total number of letters across the whole phrase.</summary>
    public static int TotalLetterCount(string? text)
        => string.IsNullOrEmpty(text) ? 0 : text.Count(char.IsLetter);

    /// <summary>First letter of each word, concatenated (a frequent geocaching follow-up step).</summary>
    public static string FirstLetters(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var word in SplitWords(text))
        {
            var letter = word.FirstOrDefault(char.IsLetter);
            if (letter != default)
            {
                builder.Append(letter);
            }
        }

        return builder.ToString();
    }

    private static string[] SplitWords(string text)
        => text.Split([' ', '\t', '-', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    // ===================== Lookup table builders =====================

    private static Dictionary<string, long> BuildEnSmallWords()
    {
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var i = 0; i < EnOnes.Length; i++)
        {
            map[EnOnes[i]] = i;
        }

        for (var i = 2; i < EnTens.Length; i++)
        {
            map[EnTens[i]] = i * 10L;
        }

        return map;
    }

    private static Dictionary<string, BigInteger> BuildEnMultipliers()
    {
        // BigInteger (not long): scales above QUINTILLION exceed long.MaxValue, so a long table would
        // overflow into garbage and let out-of-range words decode to wrong (even negative) numbers.
        var map = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
        BigInteger scale = 1000;
        for (var i = 1; i < EnScales.Length; i++)
        {
            map[EnScales[i]] = scale;
            scale *= 1000;
        }

        return map;
    }

    private static Dictionary<string, string> BuildEnOrdinalToCardinal()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (cardinal, ordinal) in EnOrdinalOnes)
        {
            map[ordinal] = cardinal;
        }

        foreach (var (cardinal, ordinal) in EnOrdinalTens)
        {
            map[ordinal] = cardinal;
        }

        // Regular -TH ordinals for the remaining ones (FOUR→FOURTH, etc.) and scale words.
        foreach (var word in EnOnes)
        {
            var ordinal = OrdinalizeWord(word);
            map.TryAdd(ordinal, word);
        }

        map["HUNDREDTH"] = "HUNDRED";
        foreach (var scale in EnScales)
        {
            if (scale.Length > 0)
            {
                map[$"{scale}TH"] = scale;
            }
        }

        return map;
    }
}
