using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class DvorakCodecTests
{
    private readonly DvorakCodec _codec = new();

    private static readonly DvorakLayout[] AllLayouts =
    [
        DvorakLayout.Qwerty,
        DvorakLayout.DvorakTwoHands,
        DvorakLayout.DvorakRightHand,
        DvorakLayout.DvorakLeftHand,
    ];

    // ---- Known QWERTY -> Simplified Dvorak letter mappings (home row) ----
    // Typing the QWERTY home-row keys A S D F G H J K L ; on a Simplified Dvorak
    // layout produces a o e u i d h t n s.

    [TestMethod]
    public void Remap_QwertyHomeRow_ToDvorak_ProducesDvorakHomeRow()
        => Assert.AreEqual("aoeuidhtns", _codec.Remap("asdfghjkl;", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));

    [TestMethod]
    public void Remap_QwertyHomeRow_ToDvorak_PreservesCase()
        => Assert.AreEqual("AOEUIDHTNS", _codec.Remap("ASDFGHJKL:", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));

    [TestMethod]
    // A few well-known individual position remaps (QWERTY key -> Dvorak char at same position).
    [DataRow('q', '\'')]
    [DataRow('w', ',')]
    [DataRow('e', '.')]
    [DataRow('r', 'p')]
    [DataRow('t', 'y')]
    [DataRow('y', 'f')]
    [DataRow('u', 'g')]
    [DataRow('i', 'c')]
    [DataRow('o', 'r')]
    [DataRow('p', 'l')]
    public void Remap_SingleLetter_QwertyToDvorak_MapsByPosition(char input, char expected)
        => Assert.AreEqual(expected.ToString(), _codec.Remap(input.ToString(), DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));

    // ---- Punctuation differences ----

    [TestMethod]
    public void Remap_Semicolon_QwertyToDvorak_BecomesS()
        // The QWERTY ';' key carries 's' on Simplified Dvorak.
        => Assert.AreEqual("s", _codec.Remap(";", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));

    [TestMethod]
    public void Remap_DvorakBracket_FromQwertyMinusKey()
        // The QWERTY '-' key carries '[' on Simplified Dvorak.
        => Assert.AreEqual("[", _codec.Remap("-", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));

    // ---- Digits are identical between QWERTY and standard Dvorak ----

    [TestMethod]
    public void Remap_Digits_QwertyToDvorak_Unchanged()
        => Assert.AreEqual("1234567890", _codec.Remap("1234567890", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));

    // ---- One-handed right-hand Dvorak known mappings (from the official layout) ----

    [TestMethod]
    public void Remap_QwertyHomeRow_ToRightHand_ProducesExpected()
        // QWERTY d f g h j k l -> z a e h t d c (letter positions per the right-hand layout image)
        => Assert.AreEqual("zaehtdc", _codec.Remap("dfghjkl", DvorakLayout.Qwerty, DvorakLayout.DvorakRightHand));

    [TestMethod]
    public void Remap_QwertyHomeRow_ToLeftHand_ProducesExpected()
        // QWERTY s d f g h j k l -> k c d t h e a z (letter positions per the left-hand layout image)
        => Assert.AreEqual("kcdtheaz", _codec.Remap("sdfghjkl", DvorakLayout.Qwerty, DvorakLayout.DvorakLeftHand));

    // ---- Pass-through of unmapped characters ----

    [TestMethod]
    public void Remap_UnmappedCharacters_PassThroughUnchanged()
    {
        // Space and a non-keyboard glyph have no key position, so they pass through verbatim.
        // QWERTY 'b' sits at the Dvorak 'x' position, so the remapped letters are a -> a, b -> x.
        var result = _codec.Remap("a b\t§", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands);
        Assert.AreEqual("a x\t§", result);
    }

    [TestMethod]
    public void Remap_SameSourceAndTarget_ReturnsInput()
    {
        const string text = "Hello, World! 123";
        Assert.AreEqual(text, _codec.Remap(text, DvorakLayout.DvorakTwoHands, DvorakLayout.DvorakTwoHands));
    }

    [TestMethod]
    public void Remap_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _codec.Remap(null, DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));
        Assert.AreEqual(string.Empty, _codec.Remap("", DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands));
    }

    // ---- Round-trip identity: the central invariant for a positional remap ----

    [TestMethod]
    [DataRow("the quick brown fox jumps over the lazy dog")]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    [DataRow("abcdefghijklmnopqrstuvwxyz")]
    [DataRow("Hello, World! It's a (test): 42% done; q=z.")]
    public void Remap_RoundTrip_QwertyDvorak_RestoresOriginal(string text)
    {
        var encoded = _codec.Remap(text, DvorakLayout.Qwerty, DvorakLayout.DvorakTwoHands);
        var decoded = _codec.Remap(encoded, DvorakLayout.DvorakTwoHands, DvorakLayout.Qwerty);
        Assert.AreEqual(text, decoded);
    }

    [TestMethod]
    public void Remap_RoundTrip_AllLayoutPairings_RestoresMappableChars()
    {
        // Build a probe from exactly the mappable key characters, so every char round-trips.
        var probe = string.Concat(_codec.GetLayout(DvorakLayout.Qwerty).Keys);

        foreach (var from in AllLayouts)
        {
            foreach (var to in AllLayouts)
            {
                var encoded = _codec.Remap(probe, from, to);
                var decoded = _codec.Remap(encoded, to, from);
                Assert.AreEqual(probe, decoded, $"round-trip failed for {from} <-> {to}");
            }
        }
    }

    // ---- Layout tables are well-formed (data-driven assertions) ----

    [TestMethod]
    [DynamicData(nameof(LayoutCases))]
    public void GetLayout_EveryLayout_HasSameKeyCountAsQwerty(DvorakLayout layout)
    {
        var qwertyCount = _codec.GetLayout(DvorakLayout.Qwerty).Keys.Count;
        Assert.AreEqual(qwertyCount, _codec.GetLayout(layout).Keys.Count);
    }

    [TestMethod]
    [DynamicData(nameof(LayoutCases))]
    public void GetLayout_EveryLayout_HasNoDuplicateKeyCharacters(DvorakLayout layout)
    {
        var keys = _codec.GetLayout(layout).Keys;
        Assert.AreEqual(keys.Count, keys.Distinct().Count(), $"{layout} has duplicate key characters");
    }

    public static IEnumerable<object[]> LayoutCases()
    {
        foreach (var layout in AllLayouts)
        {
            yield return [layout];
        }
    }
}
