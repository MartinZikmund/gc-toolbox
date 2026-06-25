using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class BaconCipherTests
{
    private readonly BaconCipher _cipher = new();

    // ---- Standard (classic 24-letter, I=J & U=V) exact table ----

    [DataTestMethod]
    [DataRow('a', "AAAAA")]
    [DataRow('b', "AAAAB")]
    [DataRow('c', "AAABA")]
    [DataRow('d', "AAABB")]
    [DataRow('e', "AABAA")]
    [DataRow('f', "AABAB")]
    [DataRow('g', "AABBA")]
    [DataRow('h', "AABBB")]
    [DataRow('i', "ABAAA")]
    [DataRow('j', "ABAAA")] // I and J share a code
    [DataRow('k', "ABAAB")]
    [DataRow('l', "ABABA")]
    [DataRow('m', "ABABB")]
    [DataRow('n', "ABBAA")]
    [DataRow('o', "ABBAB")]
    [DataRow('p', "ABBBA")]
    [DataRow('q', "ABBBB")]
    [DataRow('r', "BAAAA")]
    [DataRow('s', "BAAAB")]
    [DataRow('t', "BAABA")]
    [DataRow('u', "BAABB")]
    [DataRow('v', "BAABB")] // U and V share a code
    [DataRow('w', "BABAA")]
    [DataRow('x', "BABAB")]
    [DataRow('y', "BABBA")]
    [DataRow('z', "BABBB")]
    public void Encode_Standard_MatchesClassicTable(char letter, string expected)
        => Assert.AreEqual(expected, _cipher.Encode(letter.ToString(), BaconVersion.Standard));

    [TestMethod]
    public void Encode_StandardZ_IsBABBB()
        => Assert.AreEqual("BABBB", _cipher.Encode("z", BaconVersion.Standard));

    // ---- V2 (distinct values): index in binary, A=0..Z=25 ----

    [TestMethod]
    public void Encode_V2_A_IsAAAAA()
        => Assert.AreEqual("AAAAA", _cipher.Encode("a", BaconVersion.Distinct));

    [TestMethod]
    public void Encode_V2_Z_IsBBAAB()
        => Assert.AreEqual("BBAAB", _cipher.Encode("z", BaconVersion.Distinct));

    [DataTestMethod]
    [DataRow('a', "AAAAA")] // 0
    [DataRow('b', "AAAAB")] // 1
    [DataRow('c', "AAABA")] // 2
    [DataRow('i', "ABAAA")] // 8
    [DataRow('j', "ABAAB")] // 9 — distinct from I in V2
    [DataRow('u', "BABAA")] // 20
    [DataRow('v', "BABAB")] // 21 — distinct from U in V2
    [DataRow('y', "BBAAA")] // 24
    [DataRow('z', "BBAAB")] // 25
    public void Encode_V2_MatchesBinaryIndexTable(char letter, string expected)
        => Assert.AreEqual(expected, _cipher.Encode(letter.ToString(), BaconVersion.Distinct));

    [TestMethod]
    public void Encode_V2_EveryLetterHasUniqueCode()
    {
        var codes = new HashSet<string>();
        for (var c = 'a'; c <= 'z'; c++)
        {
            Assert.IsTrue(codes.Add(_cipher.Encode(c.ToString(), BaconVersion.Distinct)), $"duplicate code for {c}");
        }

        Assert.AreEqual(26, codes.Count);
    }

    // ---- Word encoding (groups separated by spaces) ----

    [TestMethod]
    public void Encode_Word_GroupsSeparatedBySpaces()
        => Assert.AreEqual("AAAAA AAAAB AAABA", _cipher.Encode("abc", BaconVersion.Standard));

    [TestMethod]
    public void Encode_IsCaseInsensitive()
        => Assert.AreEqual(_cipher.Encode("abc", BaconVersion.Standard), _cipher.Encode("ABC", BaconVersion.Standard));

    [TestMethod]
    public void Encode_StandardWord_HelloUsesMergedTable()
        => Assert.AreEqual("AABBB AABAA ABABA ABABA ABBAB", _cipher.Encode("hello", BaconVersion.Standard));

    // ---- Decode ----

    [TestMethod]
    public void Decode_Standard_RoundTripsSimpleWord()
        => Assert.AreEqual("ABC", _cipher.Decode(_cipher.Encode("ABC", BaconVersion.Standard), BaconVersion.Standard));

    [TestMethod]
    public void Decode_Standard_IjMergesToI()
        => Assert.AreEqual("I", _cipher.Decode("ABAAA", BaconVersion.Standard));

    [TestMethod]
    public void Decode_Standard_UvMergesToU()
        => Assert.AreEqual("U", _cipher.Decode("BAABB", BaconVersion.Standard));

    [TestMethod]
    public void Decode_V2_JIsDistinctFromI()
    {
        Assert.AreEqual("I", _cipher.Decode("ABAAA", BaconVersion.Distinct));
        Assert.AreEqual("J", _cipher.Decode("ABAAB", BaconVersion.Distinct));
    }

    [TestMethod]
    public void Decode_V2_RoundTripsFullAlphabet()
    {
        const string plain = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var encoded = _cipher.Encode(plain, BaconVersion.Distinct);
        Assert.AreEqual(plain, _cipher.Decode(encoded, BaconVersion.Distinct));
    }

    // ---- Lenient decode: ignore noise, auto-chunk into fives ----

    [TestMethod]
    public void Decode_LenientIgnoresWhitespaceAndPunctuation()
        => Assert.AreEqual("ABC", _cipher.Decode("AA-AAA / AAA,AB :: AA ABA", BaconVersion.Standard));

    [TestMethod]
    public void Decode_LenientIgnoresUnrelatedLetters()
    {
        // A hidden message disguised in a "real" sentence: only A/B carry signal,
        // every other character is dropped before chunking.
        // ABABA = L, ABABB = M; case is ignored when matching the A/B signal symbols.
        var decoded = _cipher.Decode("AbAbA AbAbB", BaconVersion.Standard);
        Assert.AreEqual("LM", decoded);
    }

    [TestMethod]
    public void Decode_NotMultipleOfFive_FlagsTrailingGroupWithQuestionMark()
        => Assert.AreEqual("A?", _cipher.Decode("AAAAA AB", BaconVersion.Standard));

    [TestMethod]
    public void Decode_UnknownGroup_MapsToQuestionMark()
    {
        // In Standard, BBBBB is not assigned to any letter.
        Assert.AreEqual("?", _cipher.Decode("BBBBB", BaconVersion.Standard));
    }

    // ---- Swap A and B ----

    [TestMethod]
    public void Encode_SwapAb_InvertsSymbols()
    {
        // a = AAAAA normally -> BBBBB when swapped.
        Assert.AreEqual("BBBBB", _cipher.Encode("a", BaconVersion.Standard, swapSymbols: true));
    }

    [TestMethod]
    public void Decode_SwapAb_RoundTrips()
    {
        var encoded = _cipher.Encode("HELLO", BaconVersion.Standard, swapSymbols: true);
        Assert.AreEqual("HELLO", _cipher.Decode(encoded, BaconVersion.Standard, swapSymbols: true));
    }

    // ---- Custom symbols ----

    [TestMethod]
    public void Encode_CustomSymbols_UsesGivenPair()
    {
        var options = new BaconOptions(BaconVersion.Standard, FirstSymbol: '0', SecondSymbol: '1');
        Assert.AreEqual("00000 00001", _cipher.Encode("ab", options));
    }

    [TestMethod]
    public void Decode_CustomSymbols_RoundTrips()
    {
        var options = new BaconOptions(BaconVersion.Distinct, FirstSymbol: '0', SecondSymbol: '1');
        var encoded = _cipher.Encode("GEOCACHE", options);
        Assert.AreEqual("GEOCACHE", _cipher.Decode(encoded, options));
    }

    [TestMethod]
    public void Encode_CustomSymbolsWithSwap_InvertsRoles()
    {
        var options = new BaconOptions(BaconVersion.Standard, FirstSymbol: '0', SecondSymbol: '1', SwapSymbols: true);
        Assert.AreEqual("11111", _cipher.Encode("a", options));
    }

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encode(text, BaconVersion.Standard));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Decode_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Decode(text, BaconVersion.Standard));

    // ---- Reference tables ----

    [TestMethod]
    public void ReferenceTable_Standard_Has26RowsAndMergesIjUv()
    {
        var table = _cipher.GetReferenceTable(BaconVersion.Standard);
        Assert.AreEqual(26, table.Count);
        Assert.AreEqual("ABAAA", table.Single(r => r.Letter == 'I').Code);
        Assert.AreEqual("ABAAA", table.Single(r => r.Letter == 'J').Code);
        Assert.AreEqual("BAABB", table.Single(r => r.Letter == 'U').Code);
        Assert.AreEqual("BAABB", table.Single(r => r.Letter == 'V').Code);
    }

    [TestMethod]
    public void ReferenceTable_V2_Has26DistinctRows()
    {
        var table = _cipher.GetReferenceTable(BaconVersion.Distinct);
        Assert.AreEqual(26, table.Count);
        Assert.AreEqual(26, table.Select(r => r.Code).Distinct().Count());
        Assert.AreEqual("AAAAA", table.Single(r => r.Letter == 'A').Code);
        Assert.AreEqual("BBAAB", table.Single(r => r.Letter == 'Z').Code);
    }
}
