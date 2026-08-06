using System.IO.Compression;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// The offline word list that resolves an ambiguous vanity code to real words — <c>34448</c> →
/// <c>DIGIT, EIGHT, FIGHT</c>, the decode geocachingtoolbox.com needs a dictionary for. Words are stored
/// in <b>frequency order</b>, so a word's index is its rank and matches come out commonest-first.
/// </summary>
/// <remarks>
/// Lookup is a binary search over <see cref="_byCode"/> — the word indices ordered by (vanity code, rank) —
/// rather than a code→words dictionary, which would cost several times the memory for 300k+ words. The
/// embedded English list is decompressed and indexed once, lazily, off the UI thread (~300 ms), and only
/// when the user actually decodes a vanity code.
/// </remarks>
public sealed class PhoneKeypadDictionary
{
    private const string EnglishResourceName = "GcToolkit.Core.Ciphers.PhoneKeypadWords-en.gz";

    private static readonly Lazy<PhoneKeypadDictionary> _english =
        new(() => new PhoneKeypadDictionary(LoadEmbeddedWords(EnglishResourceName)),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly string[] _words;
    private readonly int[] _byCode;

    /// <summary>Builds a dictionary over <paramref name="words"/>, which must already be in frequency
    /// order (commonest first). Words containing a character with no keypad key are dropped.</summary>
    public PhoneKeypadDictionary(IEnumerable<string> words)
    {
        List<string> usable = [];
        List<string> codes = [];

        foreach (var word in words)
        {
            var trimmed = word.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var upper = trimmed.ToUpperInvariant();
            if (PhoneKeypadCodec.VanityCode(upper) is not string code)
            {
                continue;
            }

            usable.Add(upper);
            codes.Add(code);
        }

        _words = [.. usable];
        _byCode = [.. Enumerable.Range(0, _words.Length)];

        // Sort against a throwaway code array (freed right after) so the comparison stays a plain
        // ordinal string compare instead of re-deriving every code on every probe.
        var keys = codes.ToArray();
        Array.Sort(
            _byCode,
            (x, y) =>
            {
                var byCode = string.CompareOrdinal(keys[x], keys[y]);
                return byCode != 0 ? byCode : x.CompareTo(y); // ties keep frequency order
            });
    }

    /// <summary>The bundled English word list (315k words, frequency-ranked).</summary>
    public static PhoneKeypadDictionary English => _english.Value;

    public int Count => _words.Length;

    /// <summary>Every dictionary word whose vanity code is exactly <paramref name="code"/>, commonest
    /// first, capped at <paramref name="max"/>.</summary>
    public IReadOnlyList<string> WordsFor(string? code, int max = 12)
    {
        if (string.IsNullOrEmpty(code) || max <= 0)
        {
            return [];
        }

        var index = LowerBound(code);
        List<string> matches = [];

        while (index < _byCode.Length && matches.Count < max && CompareCode(_words[_byCode[index]], code) == 0)
        {
            matches.Add(_words[_byCode[index]]);
            index++;
        }

        return matches;
    }

    /// <summary>The first position in <see cref="_byCode"/> whose word's code is &gt;= <paramref name="code"/>.</summary>
    private int LowerBound(string code)
    {
        var low = 0;
        var high = _byCode.Length;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (CompareCode(_words[_byCode[mid]], code) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    /// <summary>Ordinal comparison of <paramref name="word"/>'s vanity code against <paramref name="code"/>,
    /// derived digit by digit so no code string is allocated per probe.</summary>
    private static int CompareCode(string word, string code)
    {
        var shared = Math.Min(word.Length, code.Length);
        for (var i = 0; i < shared; i++)
        {
            var digit = PhoneKeypadCodec.DigitFor(word[i]);
            if (digit != code[i])
            {
                return digit.CompareTo(code[i]);
            }
        }

        return word.Length.CompareTo(code.Length);
    }

    private static IEnumerable<string> LoadEmbeddedWords(string resourceName)
    {
        using var stream = typeof(PhoneKeypadDictionary).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using GZipStream gzip = new(stream, CompressionMode.Decompress);
        using StreamReader reader = new(gzip);

        while (reader.ReadLine() is string line)
        {
            yield return line;
        }
    }
}
