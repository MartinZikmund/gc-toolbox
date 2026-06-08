using System.Globalization;

namespace GcToolkit.Core.Text;

/// <summary>
/// The numbering schemes offered by the numbers ↔ letters tool, matching geocachingtoolbox.com's
/// "Method" list: a forward/reverse base-Latin alphabet starting at 0 or 1, plus German and Nordic
/// variants that extend the alphabet with accented characters.
/// </summary>
public enum AlphabetMethod
{
    /// <summary>A=1 … Z=26.</summary>
    A1Z26,

    /// <summary>A=0 … Z=25.</summary>
    A0Z25,

    /// <summary>A=26 … Z=1 (reversed).</summary>
    A26Z1,

    /// <summary>A=25 … Z=0 (reversed).</summary>
    A25Z0,

    /// <summary>A=1 … Z=26, ä=27, ö=28, ü=29, ß=30.</summary>
    A1Z26German,

    /// <summary>A=0 … Z=25, ä=26, ö=27, ü=28, ß=29.</summary>
    A0Z25German,

    /// <summary>A=1 … Z=26, å=27, ä=28, ö=29.</summary>
    A1Z26Nordic,

    /// <summary>A=0 … Z=25, å=26, ä=27, ö=28.</summary>
    A0Z25Nordic,
}

/// <summary>A single character ↔ value pair, used to render the conversion table.</summary>
public sealed record AlphabetEntry(string Character, int Value)
{
    /// <summary>The value as a culture-invariant string, for binding to a text element.</summary>
    public string ValueText => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The character ↔ value mapping for one <see cref="AlphabetMethod"/>. Letters are keyed
/// case-insensitively (lower-case canonical); values are contiguous, so out-of-range numbers can be
/// wrapped back into the method's range with <see cref="WrapIntoRange"/>.
/// </summary>
public sealed class AlphabetMethodDefinition
{
    private readonly Dictionary<char, int> _charToValue;
    private readonly Dictionary<int, char> _valueToChar;

    internal AlphabetMethodDefinition(AlphabetMethod method, string label, string characters, int startValue, int step)
    {
        Method = method;
        Label = label;

        var entries = new List<AlphabetEntry>(characters.Length);
        _charToValue = new Dictionary<char, int>(characters.Length);
        _valueToChar = new Dictionary<int, char>(characters.Length);

        for (var i = 0; i < characters.Length; i++)
        {
            var c = characters[i];
            var value = startValue + (i * step);
            entries.Add(new AlphabetEntry(c.ToString(), value));
            _charToValue[c] = value;
            _valueToChar[value] = c;
        }

        Entries = entries;
        MinValue = _valueToChar.Keys.Min();
    }

    /// <summary>The scheme this definition implements.</summary>
    public AlphabetMethod Method { get; }

    /// <summary>The fixed, locale-neutral formula shown in the picker (e.g. <c>A=1 ... Z=26</c>).</summary>
    public string Label { get; }

    /// <summary>The character → value pairs in declaration order, for the conversion table.</summary>
    public IReadOnlyList<AlphabetEntry> Entries { get; }

    /// <summary>The smallest value in the (contiguous) range.</summary>
    public int MinValue { get; }

    /// <summary>Number of characters / size of the contiguous value range.</summary>
    public int Count => Entries.Count;

    /// <summary>Looks up the value of a letter, case-insensitively. Returns <see langword="false"/> for non-letters.</summary>
    public bool TryGetValue(char letter, out int value)
        => _charToValue.TryGetValue(char.ToLowerInvariant(letter), out value);

    /// <summary>Looks up the lower-case letter for a value. Returns <see langword="false"/> if out of range.</summary>
    public bool TryGetChar(int value, out char letter)
        => _valueToChar.TryGetValue(value, out letter);

    /// <summary>Maps any integer into the method's range via <c>min + ((n − min) mod count)</c> (handles negatives).</summary>
    public int WrapIntoRange(int n)
    {
        var m = (n - MinValue) % Count;
        if (m < 0)
        {
            m += Count;
        }

        return MinValue + m;
    }
}

/// <summary>Catalog of the eight <see cref="AlphabetMethod"/> definitions.</summary>
public static class AlphabetMethods
{
    private const string Latin = "abcdefghijklmnopqrstuvwxyz";
    private const string German = Latin + "äöüß";
    private const string Nordic = Latin + "åäö";

    private static readonly AlphabetMethodDefinition[] _all =
    [
        new(AlphabetMethod.A1Z26, "A=1 ... Z=26", Latin, startValue: 1, step: 1),
        new(AlphabetMethod.A0Z25, "A=0 ... Z=25", Latin, startValue: 0, step: 1),
        new(AlphabetMethod.A26Z1, "A=26 ... Z=1", Latin, startValue: 26, step: -1),
        new(AlphabetMethod.A25Z0, "A=25 ... Z=0", Latin, startValue: 25, step: -1),
        new(AlphabetMethod.A1Z26German, "A=1 ... Z=26, ä=27, ö=28, ü=29, ß=30", German, startValue: 1, step: 1),
        new(AlphabetMethod.A0Z25German, "A=0 ... Z=25, ä=26, ö=27, ü=28, ß=29", German, startValue: 0, step: 1),
        new(AlphabetMethod.A1Z26Nordic, "A=1 ... Z=26, å=27, ä=28, ö=29", Nordic, startValue: 1, step: 1),
        new(AlphabetMethod.A0Z25Nordic, "A=0 ... Z=25, å=26, ä=27, ö=28", Nordic, startValue: 0, step: 1),
    ];

    /// <summary>All method definitions in display order (matches the <see cref="AlphabetMethod"/> declaration order).</summary>
    public static IReadOnlyList<AlphabetMethodDefinition> All { get; } = _all;

    private static readonly Dictionary<AlphabetMethod, AlphabetMethodDefinition> _byMethod =
        _all.ToDictionary(d => d.Method);

    /// <summary>The definition for a given method.</summary>
    public static AlphabetMethodDefinition Get(AlphabetMethod method) => _byMethod[method];
}
