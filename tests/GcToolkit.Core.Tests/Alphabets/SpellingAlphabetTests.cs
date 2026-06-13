using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SpellingAlphabetTests
{
    private readonly SpellingAlphabetCodec _codec = new();

    private static SpellingAlphabetVariant Nato
        => SpellingAlphabets.ById("Nato");

    // ---- Variant catalog ----

    [TestMethod]
    public void All_ContainsTheElevenVariants()
        => Assert.AreEqual(11, SpellingAlphabets.All.Count);

    [TestMethod]
    public void All_HasUniqueIds()
    {
        var ids = SpellingAlphabets.All.Select(v => v.Id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
    }

    [TestMethod]
    public void Default_IsNato()
        => Assert.AreEqual("Nato", SpellingAlphabets.Default.Id);

    // ---- NATO canonical table ----

    [TestMethod]
    public void Nato_FullAlphabet_MatchesCanonicalWords()
    {
        string[] expected =
        [
            "Alfa", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India",
            "Juliett", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa", "Quebec", "Romeo",
            "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "X-ray", "Yankee", "Zulu",
        ];

        for (var i = 0; i < 26; i++)
        {
            var letter = (char)('A' + i);
            Assert.IsTrue(Nato.TryGetWord(letter, out var word), $"NATO missing {letter}");
            Assert.AreEqual(expected[i], word);
        }
    }

    [TestMethod]
    public void Nato_HasDigitWords()
    {
        Assert.IsTrue(Nato.TryGetWord('0', out var zero));
        Assert.AreEqual("Zero", zero);
        Assert.IsTrue(Nato.TryGetWord('9', out var nine));
        Assert.AreEqual("Niner", nine);
    }

    // ---- Per-variant distinctive code words (verified against authoritative sources) ----
    // These guard against wrong/fabricated words, which a round-trip test cannot catch because it
    // encodes and decodes with the same table.

    [TestMethod]
    [DataRow("Itu1932", 'A', "Amsterdam")]
    [DataRow("Itu1932", 'K', "Kilogramme")]
    [DataRow("Itu1932", 'U', "Upsala")]
    [DataRow("Itu1932", 'X', "Xanthippe")]
    [DataRow("WesternUnion", 'A', "Adams")]
    [DataRow("WesternUnion", 'R', "Roger")]
    [DataRow("WesternUnion", 'T', "Thomas")]
    [DataRow("WesternUnion", 'Y', "Young")]
    [DataRow("WesternUnion", 'Z', "Zero")]
    [DataRow("AbleBaker", 'A', "Able")]
    [DataRow("AbleBaker", 'B', "Baker")]
    [DataRow("AbleBaker", 'O', "Oboe")]
    [DataRow("AbleBaker", 'T', "Tare")]
    [DataRow("Raf1924", 'A', "Ace")]
    [DataRow("Raf1924", 'B', "Beer")]
    [DataRow("Raf1924", 'T', "Toc")]
    [DataRow("Raf1924", 'V', "Vic")]
    [DataRow("Apco", 'A', "Adam")]
    [DataRow("Apco", 'O', "Ocean")]
    [DataRow("Apco", 'P', "Paul")]
    [DataRow("Apco", 'Z', "Zebra")]
    [DataRow("Dutch", 'A', "Anton")]
    [DataRow("Dutch", 'B', "Bernard")]   // Dutch B is "Bernard", not the German "Bernhard"
    [DataRow("Dutch", 'Q', "Quotiënt")]
    [DataRow("Dutch", 'Z', "Zaandam")]
    [DataRow("German", 'C', "Cäsar")]
    [DataRow("German", 'K', "Kaufmann")]
    [DataRow("German", 'N', "Nordpol")]
    [DataRow("German", 'S', "Samuel")]
    [DataRow("German", 'Z', "Zacharias")]
    [DataRow("Swedish", 'A', "Adam")]
    [DataRow("Swedish", 'C', "Cesar")]    // corrected: was "Caesar"
    [DataRow("Swedish", 'Q', "Qvintus")]  // corrected: was "Quintus"
    [DataRow("Swedish", 'X', "Xerxes")]
    [DataRow("Swedish", 'Z', "Zäta")]
    public void Variant_DistinctiveLatinWords_MatchAuthoritativeSources(string id, char letter, string expected)
    {
        var variant = SpellingAlphabets.ById(id);
        Assert.IsTrue(variant.TryGetWord(letter, out var word), $"{id} missing {letter}");
        Assert.AreEqual(expected, word);
    }

    [TestMethod]
    public void Swedish_CAndQ_AreCesarAndQvintus_NotTheGermanOrLatinForms()
    {
        var swedish = SpellingAlphabets.ById("Swedish");

        Assert.IsTrue(swedish.TryGetWord('C', out var c));
        Assert.AreEqual("Cesar", c);
        Assert.AreNotEqual("Caesar", c);

        Assert.IsTrue(swedish.TryGetWord('Q', out var q));
        Assert.AreEqual("Qvintus", q);
        Assert.AreNotEqual("Quintus", q);
    }

    [TestMethod]
    [DataRow("RussianOfficial", 'Г', "Grigoriy")]
    [DataRow("RussianOfficial", 'Й', "Ivan kratkiy")]
    [DataRow("RussianOfficial", 'К', "Konstantin")]
    [DataRow("RussianOfficial", 'Р', "Roman")]
    [DataRow("RussianOfficial", 'С', "Semyon")]
    [DataRow("RussianOfficial", 'Ц', "Tsaplya")]
    [DataRow("RussianUnofficial", 'Г', "Galina")]
    [DataRow("RussianUnofficial", 'Й', "Yot")]
    [DataRow("RussianUnofficial", 'К', "Kilovatt")]
    [DataRow("RussianUnofficial", 'Р', "Radio")]
    [DataRow("RussianUnofficial", 'С', "Sergey")]
    [DataRow("RussianUnofficial", 'Ц', "Tsentr")]
    public void Variant_DistinctiveCyrillicWords_MatchAuthoritativeSources(string id, char letter, string expected)
    {
        var variant = SpellingAlphabets.ById(id);
        Assert.IsTrue(variant.TryGetWord(letter, out var word), $"{id} missing {letter}");
        Assert.AreEqual(expected, word);
    }

    // ---- Encode ----

    [TestMethod]
    public void Encode_Geo_ProducesGolfEchoOscar()
        => Assert.AreEqual("Golf Echo Oscar", _codec.Encode("GEO", Nato));

    [TestMethod]
    public void Encode_IsCaseInsensitive()
        => Assert.AreEqual("Golf Echo Oscar", _codec.Encode("geo", Nato));

    [TestMethod]
    public void Encode_KeepsWordsSeparatedByDoubleSpace()
    {
        var result = _codec.Encode("GC HQ", Nato);
        Assert.AreEqual("Golf Charlie  Hotel Quebec", result);
    }

    [TestMethod]
    public void Encode_UnmappedCharacter_IsSkipped()
    {
        // Punctuation has no code word in NATO and is dropped.
        var result = _codec.Encode("A!B", Nato);
        Assert.AreEqual("Alfa Bravo", result);
    }

    // ---- Decode ----

    [TestMethod]
    public void Decode_GolfEchoOscar_ProducesGeo()
        => Assert.AreEqual("GEO", _codec.Decode("GOLF ECHO OSCAR", Nato).Text);

    [TestMethod]
    public void Decode_IsCaseInsensitive()
        => Assert.AreEqual("GEO", _codec.Decode("golf echo oscar", Nato).Text);

    [TestMethod]
    [DataRow("Alpha", "A")]
    [DataRow("Alfa", "A")]
    [DataRow("Juliet", "J")]
    [DataRow("Juliett", "J")]
    [DataRow("Xray", "X")]
    [DataRow("X-ray", "X")]
    public void Decode_TolerantOfCommonAliases(string word, string expected)
        => Assert.AreEqual(expected, _codec.Decode(word, Nato).Text);

    [TestMethod]
    public void Decode_AcceptsCommaSeparators()
        => Assert.AreEqual("GEO", _codec.Decode("Golf, Echo, Oscar", Nato).Text);

    [TestMethod]
    public void Decode_DoubleSpace_ProducesWordGap()
        => Assert.AreEqual("GC HQ", _codec.Decode("Golf Charlie  Hotel Quebec", Nato).Text);

    [TestMethod]
    public void Decode_UnknownWord_IsFlaggedNotCrashed()
    {
        var result = _codec.Decode("Golf Banana Oscar", Nato);

        Assert.IsTrue(result.HasUnknown);
        CollectionAssert.Contains(result.UnknownWords.ToArray(), "Banana");
        // Known words still decode; the unknown is marked with a placeholder.
        StringAssert.Contains(result.Text, "G");
        StringAssert.Contains(result.Text, "O");
    }

    // ---- Round trip for every implemented variant ----

    [TestMethod]
    public void RoundTrip_EveryVariant_RecoversTheLetters()
    {
        foreach (var variant in SpellingAlphabets.All)
        {
            // Build a sample from this variant's own keys so Cyrillic variants are covered too.
            var sample = new string(variant.Keys.Take(5).ToArray());
            var encoded = _codec.Encode(sample, variant);
            var decoded = _codec.Decode(encoded, variant);

            Assert.AreEqual(
                sample.ToUpperInvariant(),
                decoded.Text,
                $"Round trip failed for variant '{variant.Id}' (encoded: '{encoded}').");
            Assert.IsFalse(decoded.HasUnknown, $"Variant '{variant.Id}' reported unknown words on round trip.");
        }
    }

    [TestMethod]
    public void EveryVariant_CoversTwentySixLatinLettersOrFullCyrillic()
    {
        foreach (var variant in SpellingAlphabets.All)
        {
            if (variant.IsCyrillic)
            {
                Assert.IsTrue(variant.Chart.Count >= 30, $"Cyrillic variant '{variant.Id}' incomplete.");
            }
            else
            {
                foreach (var letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
                {
                    Assert.IsTrue(variant.TryGetWord(letter, out _), $"Variant '{variant.Id}' missing {letter}.");
                }
            }
        }
    }

    // ---- Auto-detect direction (beyond parity) ----

    [TestMethod]
    public void LooksLikeCodeWords_PlainText_IsFalse()
        => Assert.IsFalse(_codec.LooksLikeCodeWords("Hello world", Nato));

    [TestMethod]
    public void LooksLikeCodeWords_CodeWordSequence_IsTrue()
        => Assert.IsTrue(_codec.LooksLikeCodeWords("Golf Echo Oscar", Nato));
}
