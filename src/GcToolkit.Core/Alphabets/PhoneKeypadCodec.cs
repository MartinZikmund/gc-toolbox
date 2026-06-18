using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>One key on the classic ITU E.161 phone keypad: its <see cref="Digit"/> and the
/// <see cref="Letters"/> it cycles through (empty for 0/1, whose roles are space/symbols).</summary>
public sealed record PhoneKey(char Digit, string Letters);

/// <summary>
/// A pure, stateless codec for the three classic mobile-phone keypad conventions used in geocaching
/// puzzles, built on the ITU&#160;E.161 mapping (2=ABC 3=DEF 4=GHI 5=JKL 6=MNO 7=PQRS 8=TUV 9=WXYZ,
/// 0=space, 1=symbols):
/// <list type="bullet">
/// <item><b>Multi-tap</b> — repeat a key to reach the n-th letter: A=2, B=22, C=222, so "CODE" =
/// "222-666-3-33". A configurable separator groups the per-letter taps; decode reverses it.</item>
/// <item><b>Key+position</b> — explicit <c>key-position</c> pairs: "7-3" = R (3rd letter of key 7).</item>
/// <item><b>T9 predictive</b> — one digit per letter ("36" could be DM, DN, DO, EM, …); enumerating
/// every letter combination is the deterministic baseline (dictionary ranking is a stretch goal).</item>
/// </list>
/// Going beyond cachesleuth.com's decode-only predictive tool, this codec encodes <em>and</em> decodes
/// all three modes and is fully offline.
/// </summary>
public sealed class PhoneKeypadCodec
{
    /// <summary>The default separator between per-letter tap groups ("222-666-3-33").</summary>
    public const char DefaultSeparator = '-';

    /// <summary>The digit that represents a word break / space in multi-tap and predictive input.</summary>
    public const char SpaceDigit = '0';

    /// <summary>Letters per key, indexed by digit. 0 and 1 carry no letters.</summary>
    private static readonly IReadOnlyDictionary<char, string> _lettersByDigit = new Dictionary<char, string>
    {
        ['2'] = "ABC",
        ['3'] = "DEF",
        ['4'] = "GHI",
        ['5'] = "JKL",
        ['6'] = "MNO",
        ['7'] = "PQRS",
        ['8'] = "TUV",
        ['9'] = "WXYZ",
        ['0'] = "",
        ['1'] = "",
    };

    /// <summary>For each upper-case letter, the digit it lives on and its 1-based tap count.</summary>
    private static readonly IReadOnlyDictionary<char, (char Digit, int Taps)> _letterInfo = BuildLetterInfo();

    /// <summary>The on-screen keypad rows (0 sits between * and #, mirroring a real phone): 1-9 then 0.</summary>
    public static IReadOnlyList<PhoneKey> Keypad { get; } =
    [
        .. "123456789".Select(d => new PhoneKey(d, _lettersByDigit[d])),
        new PhoneKey('0', _lettersByDigit['0']),
    ];

    // ---- Multi-tap ----

    /// <summary>
    /// Encodes <paramref name="text"/> to multi-tap groups joined by <paramref name="separator"/>:
    /// each letter becomes its key repeated by tap count, a space becomes <see cref="SpaceDigit"/>.
    /// Letters outside A–Z (after case-folding) are skipped. Example: "CODE" → "222-666-3-33".
    /// </summary>
    public string EncodeMultitap(string? text, char separator = DefaultSeparator)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        List<string> groups = [];
        foreach (var ch in text)
        {
            var upper = char.ToUpperInvariant(ch);
            if (upper == ' ')
            {
                groups.Add(SpaceDigit.ToString());
            }
            else if (_letterInfo.TryGetValue(upper, out var info))
            {
                groups.Add(new string(info.Digit, info.Taps));
            }
        }

        return string.Join(separator, groups);
    }

    /// <summary>
    /// Decodes multi-tap <paramref name="code"/> back to text. Groups are separated by
    /// <paramref name="separator"/>, whitespace, or a key change within an un-separated run
    /// ("22 7" or "227" or "22-7" all decode to "BP"). A lone <see cref="SpaceDigit"/> becomes a space.
    /// Over-long runs wrap around the key's letters (e.g. "7777 7" stays valid).
    /// </summary>
    public string DecodeMultitap(string? code, char separator = DefaultSeparator)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        // Any non-digit (the separator, whitespace, or stray punctuation) ends a group, so the
        // explicit separator no longer needs threading into the splitter.
        _ = separator;

        StringBuilder result = new();
        foreach (var group in SplitTapGroups(code))
        {
            if (group.Length == 0)
            {
                continue;
            }

            var digit = group[0];
            if (digit == SpaceDigit)
            {
                result.Append(' ');
                continue;
            }

            if (_lettersByDigit.TryGetValue(digit, out var letters) && letters.Length > 0)
            {
                // A run of one key cycles its letters; wrap so over-tapping never throws.
                result.Append(letters[(group.Length - 1) % letters.Length]);
            }
        }

        return result.ToString();
    }

    // ---- Key + position pairs ----

    /// <summary>
    /// Encodes <paramref name="text"/> to explicit <c>key-position</c> pairs (separated by spaces):
    /// R → "7-3". Spaces become <see cref="SpaceDigit"/>; out-of-alphabet letters are skipped.
    /// </summary>
    public string EncodeKeyPosition(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        List<string> pairs = [];
        foreach (var ch in text)
        {
            var upper = char.ToUpperInvariant(ch);
            if (upper == ' ')
            {
                pairs.Add(SpaceDigit.ToString());
            }
            else if (_letterInfo.TryGetValue(upper, out var info))
            {
                pairs.Add($"{info.Digit}{DefaultSeparator}{info.Taps}");
            }
        }

        return string.Join(' ', pairs);
    }

    /// <summary>
    /// Decodes <c>key-position</c> pairs back to text. Pairs are whitespace- or comma-separated and use
    /// the form <c>digit-position</c> ("7-3" = R); a bare <see cref="SpaceDigit"/> token is a space.
    /// Out-of-range positions and malformed tokens are skipped.
    /// </summary>
    public string DecodeKeyPosition(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        StringBuilder result = new();
        foreach (var token in code.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == SpaceDigit.ToString())
            {
                result.Append(' ');
                continue;
            }

            var parts = token.Split(DefaultSeparator);
            if (parts.Length == 2
                && parts[0].Length == 1
                && _lettersByDigit.TryGetValue(parts[0][0], out var letters)
                && letters.Length > 0
                && int.TryParse(parts[1], out var position)
                && position >= 1
                && position <= letters.Length)
            {
                result.Append(letters[position - 1]);
            }
        }

        return result.ToString();
    }

    // ---- T9 predictive ----

    /// <summary>
    /// Encodes <paramref name="text"/> to a T9 predictive digit sequence — one digit per letter, with
    /// spaces preserved as <see cref="SpaceDigit"/>. "GEOCACHE" → "43622243".
    /// </summary>
    public string EncodeT9(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder result = new();
        foreach (var ch in text)
        {
            var upper = char.ToUpperInvariant(ch);
            if (upper == ' ')
            {
                result.Append(SpaceDigit);
            }
            else if (_letterInfo.TryGetValue(upper, out var info))
            {
                result.Append(info.Digit);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Enumerates every letter combination for a single T9 token (a run with no spaces). Each digit
    /// contributes its key's letters; the cartesian product is returned in stable order. Digits with
    /// no letters (0/1) and non-digit characters are ignored. Capped at <paramref name="limit"/>
    /// results to stay bounded (default 5,000).
    /// </summary>
    public IReadOnlyList<string> EnumerateT9Token(string? digits, int limit = 5000)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return [];
        }

        // Only digits that carry letters expand the product; skip 0/1 and stray characters.
        List<string> options = [];
        foreach (var ch in digits)
        {
            if (_lettersByDigit.TryGetValue(ch, out var letters) && letters.Length > 0)
            {
                options.Add(letters);
            }
        }

        if (options.Count == 0)
        {
            return [];
        }

        // Build the full cartesian product in prefix-major order, capping each level at `limit`
        // entries. Because the product is prefix-major, the surviving first `limit` prefixes at every
        // level are exactly the prefixes of the first `limit` complete combinations — so the returned
        // list is the first `limit` full-length results in stable order, never a partial-length string.
        List<string> results = ["",];
        foreach (var letters in options)
        {
            List<string> next = new(Math.Min(results.Count * letters.Length, limit));
            foreach (var prefix in results)
            {
                foreach (var letter in letters)
                {
                    next.Add(prefix + letter);
                }

                if (next.Count >= limit)
                {
                    break;
                }
            }

            if (next.Count > limit)
            {
                next.RemoveRange(limit, next.Count - limit);
            }

            results = next;
        }

        return results;
    }

    /// <summary>
    /// Splits a multi-word T9 sequence on <paramref name="separator"/> (and whitespace) into per-word
    /// tokens, preserving order. The default separator is <see cref="SpaceDigit"/>. Empty tokens are dropped.
    /// </summary>
    public IReadOnlyList<string> SplitWords(string? sequence, char separator = SpaceDigit)
    {
        if (string.IsNullOrWhiteSpace(sequence))
        {
            return [];
        }

        var separators = separator == SpaceDigit
            ? [SpaceDigit, ' ', '\t', '\r', '\n']
            : new[] { separator, ' ', '\t', '\r', '\n' };

        return [.. sequence.Split(separators, StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>The letters carried by <paramref name="digit"/> (empty for 0/1 and unknown characters).</summary>
    public string LettersFor(char digit)
        => _lettersByDigit.TryGetValue(digit, out var letters) ? letters : string.Empty;

    /// <summary>
    /// Splits a multi-tap string into per-letter groups. Any non-digit (separator, whitespace, stray
    /// punctuation) ends a group, and — for un-separated digit runs — a key change also breaks, so
    /// "227" yields "22" then "7".
    /// </summary>
    private static IEnumerable<string> SplitTapGroups(string code)
    {
        StringBuilder current = new();
        foreach (var ch in code)
        {
            // The separator, whitespace, and any stray punctuation all end the current group —
            // punctuation must break a run ("3.33" is E then F), never be swallowed into it.
            if (!char.IsDigit(ch))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                continue;
            }

            // A key change inside an un-separated run starts a new group.
            if (current.Length > 0 && current[^1] != ch)
            {
                yield return current.ToString();
                current.Clear();
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static Dictionary<char, (char Digit, int Taps)> BuildLetterInfo()
    {
        Dictionary<char, (char, int)> map = new();
        foreach (var (digit, letters) in _lettersByDigit)
        {
            for (var i = 0; i < letters.Length; i++)
            {
                map[letters[i]] = (digit, i + 1);
            }
        }

        return map;
    }
}
