using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class KennyCodeCodecTests
{
    private readonly KennyCodeCodec _codec = new();

    // ---- The exact 26-letter substitution table (geocachingtoolbox.com) ----

    [DataTestMethod]
    [DataRow('a', "mmm")]
    [DataRow('b', "mmp")]
    [DataRow('c', "mmf")]
    [DataRow('d', "mpm")]
    [DataRow('e', "mpp")]
    [DataRow('f', "mpf")]
    [DataRow('g', "mfm")]
    [DataRow('h', "mfp")]
    [DataRow('i', "mff")]
    [DataRow('j', "pmm")]
    [DataRow('k', "pmp")]
    [DataRow('l', "pmf")]
    [DataRow('m', "ppm")]
    [DataRow('n', "ppp")]
    [DataRow('o', "ppf")]
    [DataRow('p', "pfm")]
    [DataRow('q', "pfp")]
    [DataRow('r', "pff")]
    [DataRow('s', "fmm")]
    [DataRow('t', "fmp")]
    [DataRow('u', "fmf")]
    [DataRow('v', "fpm")]
    [DataRow('w', "fpp")]
    [DataRow('x', "fpf")]
    [DataRow('y', "ffm")]
    [DataRow('z', "ffp")]
    public void Encode_SingleLetter_ProducesExactTrigram(char letter, string expected)
        => Assert.AreEqual(expected, _codec.Encode(letter.ToString()));

    [TestMethod]
    public void ReferenceTable_MatchesVerifiedTable()
    {
        string[] expected =
        [
            "mmm", "mmp", "mmf", "mpm", "mpp", "mpf", "mfm", "mfp", "mff", "pmm", "pmp", "pmf", "ppm",
            "ppp", "ppf", "pfm", "pfp", "pff", "fmm", "fmp", "fmf", "fpm", "fpp", "fpf", "ffm", "ffp",
        ];

        Assert.AreEqual(26, _codec.ReferenceTable.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual((char)('A' + i), _codec.ReferenceTable[i].Letter, $"row {i} letter");
            Assert.AreEqual(expected[i], _codec.ReferenceTable[i].Trigram, $"row {i} trigram");
        }
    }

    // ---- Encode ----

    [TestMethod]
    public void Encode_WordWithDefaultOptions_DropsSpacingAndJoins()
    {
        // Default: lower case, no separator, structure not preserved. g=mfm e=mpp o=ppf
        Assert.AreEqual("mfmmppppf", _codec.Encode("geo"));
    }

    [TestMethod]
    public void Encode_UpperCaseOption_ProducesUpperTrigrams()
    {
        var options = new KennyCodeEncodeOptions { UpperCase = true };
        Assert.AreEqual("MMMMMPMMF", _codec.Encode("abc", options));
    }

    [TestMethod]
    public void Encode_IsCaseInsensitive()
        => Assert.AreEqual(_codec.Encode("abc"), _codec.Encode("ABC"));

    [DataTestMethod]
    [DataRow(KennyCodeSeparator.Space, "mmm mmp mmf")]
    [DataRow(KennyCodeSeparator.Comma, "mmm,mmp,mmf")]
    [DataRow(KennyCodeSeparator.None, "mmmmmpmmf")]
    public void Encode_Separator_JoinsTrigramsAccordingly(KennyCodeSeparator separator, string expected)
    {
        var options = new KennyCodeEncodeOptions { Separator = separator };
        Assert.AreEqual(expected, _codec.Encode("abc", options));
    }

    [TestMethod]
    public void Encode_DefaultDropsNonLetters()
        => Assert.AreEqual(_codec.Encode("ab"), _codec.Encode("a-b! 1"));

    [TestMethod]
    public void Encode_PreserveStructure_KeepsSpacesBetweenWords()
    {
        var options = new KennyCodeEncodeOptions { Separator = KennyCodeSeparator.Space, PreserveStructure = true };
        // "a b" -> trigrams space-separated within a word, words separated by a wider gap.
        var result = _codec.Encode("a b", options);
        Assert.IsTrue(result.Contains("mmm"));
        Assert.IsTrue(result.Contains("mmp"));
        // The two words must remain distinguishable (a gap survives between them).
        StringAssert.Contains(result, " ");
    }

    [TestMethod]
    public void Encode_NoneSeparator_PreserveStructureToggle_ChangesOutputObservably()
    {
        // With separator None the word gap is the ONLY thing PreserveStructure can add, so the option is
        // observable here (unlike with Space, where intra-word and inter-word joins are both a space).
        // a=mmm b=mmp. Preserving the inter-word gap of "a b" keeps the two trigrams apart.
        var preserve = new KennyCodeEncodeOptions { Separator = KennyCodeSeparator.None, PreserveStructure = true };
        var collapse = new KennyCodeEncodeOptions { Separator = KennyCodeSeparator.None, PreserveStructure = false };

        var preserved = _codec.Encode("a b", preserve);
        var collapsed = _codec.Encode("a b", collapse);

        Assert.AreEqual("mmm mmp", preserved);
        Assert.AreEqual("mmmmmp", collapsed);
        // The two outputs must differ — proof that the PreserveStructure branch actually fires.
        Assert.AreNotEqual(collapsed, preserved);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.Encode(text));

    // ---- Decode (lenient) ----

    [TestMethod]
    public void Decode_PlainTrigrams_ReturnsLetters()
        => Assert.AreEqual("geo", _codec.Decode("mfmmppppf").Text);

    [TestMethod]
    public void Decode_IsCaseInsensitive()
        => Assert.AreEqual("abc", _codec.Decode("MMMMMPMMF").Text);

    [DataTestMethod]
    [DataRow("mmm mmp mmf")]
    [DataRow("mmm,mmp,mmf")]
    [DataRow("mmm, mmp, mmf")]
    [DataRow("m m m\nm m p\tm m f")]
    [DataRow("xMMMx?MMPyMMFz")] // noise that is not m/p/f is stripped
    public void Decode_StripsSeparatorsAndNoise_ReturnsAbc(string encoded)
        => Assert.AreEqual("abc", _codec.Decode(encoded).Text);

    [TestMethod]
    public void Decode_RoundTripsTheWholeAlphabet()
    {
        const string plain = "abcdefghijklmnopqrstuvwxyz";
        var encoded = _codec.Encode(plain);
        Assert.AreEqual(plain, _codec.Decode(encoded).Text);
    }

    [TestMethod]
    public void Decode_RoundTripsGeocache()
    {
        var encoded = _codec.Encode("geocache");
        var result = _codec.Decode(encoded);
        Assert.AreEqual("geocache", result.Text);
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(0, result.InvalidGroupCount);
    }

    [TestMethod]
    public void Decode_NotMultipleOfThree_ReportsInvalidTrailingGroup()
    {
        // "mmm" + "mp" — the trailing pair is incomplete.
        var result = _codec.Decode("mmmmp");
        Assert.IsTrue(result.HasErrors);
        Assert.IsFalse(result.IsLengthMultipleOfThree);
        Assert.AreEqual(1, result.InvalidGroupCount);
        StringAssert.StartsWith(result.Text, "a");
        StringAssert.EndsWith(result.Text, "?");
    }

    [TestMethod]
    public void Decode_AllGroupsValid_HasNoErrorsAndLengthIsMultipleOfThree()
    {
        var result = _codec.Decode("mmmmmp");
        Assert.AreEqual("ab", result.Text);
        Assert.IsFalse(result.HasErrors);
        Assert.IsTrue(result.IsLengthMultipleOfThree);
        Assert.AreEqual(0, result.InvalidGroupCount);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("xyz123 ?!")] // no m/p/f letters at all
    public void Decode_NoSignal_ReturnsEmptyWithoutErrors(string? text)
    {
        var result = _codec.Decode(text);
        Assert.AreEqual(string.Empty, result.Text);
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(0, result.InvalidGroupCount);
    }
}
