namespace GcToolkit.Core.Text;

/// <summary>
/// The character-to-number assignment used by the word-value tool. Mirrors the methods offered by
/// geocachingtoolbox.com (with the same letter-value tables) plus the diacritic-extended schemes for
/// German (ä/ö/ü/ß) and Swedish (å/ä/ö). The declaration order is the dropdown order.
/// </summary>
public enum WordValueMethod
{
    /// <summary>A=1 … Z=26 (the classic alphabet position).</summary>
    A1Z26,

    /// <summary>A=0 … Z=25 (zero-based).</summary>
    A0Z25,

    /// <summary>A=26 … Z=1 (reversed).</summary>
    A26Z1,

    /// <summary>A=25 … Z=0 (reversed, zero-based).</summary>
    A25Z0,

    /// <summary>A=2 … Z=9 — the telephone keypad ("vanity code") grouping.</summary>
    Vanity,

    /// <summary>Scrabble tile values, Dutch edition.</summary>
    ScrabbleDutch,

    /// <summary>Scrabble tile values, English edition.</summary>
    ScrabbleEnglish,

    /// <summary>Scrabble tile values, German edition.</summary>
    ScrabbleGerman,

    /// <summary>Repeating 0-9 table (A=0 … J=9, K=0 …).</summary>
    Table0to9,

    /// <summary>Repeating 1-0 table (A=1 … J=0, K=1 …).</summary>
    Table1to0,

    /// <summary>Repeating 1-9 table (A=1 … I=9, J=1 …).</summary>
    Table1to9,

    /// <summary>A=1 … Z=26, ä=27, ö=28, ü=29, ß=30 (German, accents significant).</summary>
    German1,

    /// <summary>A=0 … Z=25, ä=26, ö=27, ü=28, ß=29 (German, zero-based, accents significant).</summary>
    German0,

    /// <summary>A=1 … Z=26, å=27, ä=28, ö=29 (Swedish, accents significant).</summary>
    Swedish1,

    /// <summary>A=0 … Z=25, å=26, ä=27, ö=28 (Swedish, zero-based, accents significant).</summary>
    Swedish0,
}

/// <summary>How plain digits in the input are treated when computing the word value.</summary>
public enum NumberHandling
{
    /// <summary>Digits are ignored entirely.</summary>
    Ignore,

    /// <summary>Each digit's face value is added inline to the single total (geocachingtoolbox.com behavior).</summary>
    DigitsInTotal,

    /// <summary>Multi-digit numbers are summed in a separate total with their own digital root.</summary>
    NumbersSeparate,
}
