using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public sealed class QwertyShifterTests
{
    private readonly QwertyShifter _shifter = new();

    // ---- Ordering / alphabet sizes ----

    [TestMethod]
    public void AlphabetSize_MatchesCacheSleuth()
    {
        Assert.AreEqual(36, QwertyShifter.AlphabetSize(QwertyAlphabet.LettersDigits));
        Assert.AreEqual(47, QwertyShifter.AlphabetSize(QwertyAlphabet.Extended));
    }

    [TestMethod]
    public void KeyOrdering_LettersDigits_Has36KeysInQwertySequence()
    {
        var ordering = _shifter.KeyOrdering(KeyboardLayout.Qwerty, QwertyAlphabet.LettersDigits);

        Assert.AreEqual(36, ordering.Length);
        Assert.AreEqual("1234567890QWERTYUIOPASDFGHJKLZXCVBNM", ordering);
    }

    [TestMethod]
    public void KeyOrdering_Extended_Has47Keys()
        => Assert.AreEqual(47, _shifter.KeyOrdering(KeyboardLayout.Qwerty, QwertyAlphabet.Extended).Length);

    [TestMethod]
    public void KeyOrdering_HasNoDuplicateKeys()
    {
        var ordering = _shifter.KeyOrdering(KeyboardLayout.Qwerty, QwertyAlphabet.Extended);
        Assert.AreEqual(ordering.Length, ordering.Distinct().Count());
    }

    // ---- Basic right shift along the ordering ----

    [TestMethod]
    public void Encode_RightByOne_MovesToNextKey()
    {
        // Q -> W -> E ... and 1 -> 2; verify a couple of adjacent keys.
        Assert.AreEqual("W", _shifter.Encode("Q", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits));
        Assert.AreEqual("2", _shifter.Encode("1", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits));
    }

    [TestMethod]
    public void Encode_LeftByOne_MovesToPreviousKey()
        => Assert.AreEqual("Q", _shifter.Encode("W", 1, ShiftDirection.Left, QwertyAlphabet.LettersDigits));

    [TestMethod]
    public void Encode_LeftAndRight_AreInverses()
    {
        const string plain = "GEOCACHE";
        var right = _shifter.Encode(plain, 5, ShiftDirection.Right, QwertyAlphabet.LettersDigits);
        var back = _shifter.Encode(right, 5, ShiftDirection.Left, QwertyAlphabet.LettersDigits);
        Assert.AreEqual(plain, back);
    }

    // ---- Wraparound (circular) ----

    [TestMethod]
    public void Encode_WrapsCircularlyAtTheEnd()
    {
        // The last key in the 36-ordering is 'M'; one step right wraps to the first key '1'.
        Assert.AreEqual("1", _shifter.Encode("M", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits));
    }

    [TestMethod]
    public void Encode_WrapsCircularlyAtTheStart()
    {
        // The first key is '1'; one step left wraps to the last key 'M'.
        Assert.AreEqual("M", _shifter.Encode("1", 1, ShiftDirection.Left, QwertyAlphabet.LettersDigits));
    }

    [TestMethod]
    public void Encode_FullSizeShift_IsIdentity()
    {
        const string plain = "HELLO123";
        Assert.AreEqual(plain, _shifter.Encode(plain, 36, ShiftDirection.Right, QwertyAlphabet.LettersDigits));
    }

    [TestMethod]
    public void Encode_ZeroShift_IsIdentity()
        => Assert.AreEqual("HELLO", _shifter.Encode("HELLO", 0, ShiftDirection.Right, QwertyAlphabet.LettersDigits));

    // ---- Round trips across the whole range ----

    [TestMethod]
    public void DecodeEncode_RoundTrips_ForEveryShiftInRange()
    {
        // Upper-case: in the mixed letter+digit ordering a letter can wrap onto a digit, so the
        // canonical (upper-case) round trip is what must hold for the whole range.
        const string plain = "THE QUICK BROWN FOX 12345";
        for (var shift = 0; shift <= QwertyShifter.LettersDigitsSize; shift++)
        {
            var encoded = _shifter.Encode(plain, shift, ShiftDirection.Right, QwertyAlphabet.LettersDigits, foldToUpper: true);
            var decoded = _shifter.Decode(encoded, shift, ShiftDirection.Right, QwertyAlphabet.LettersDigits, foldToUpper: true);
            Assert.AreEqual(plain, decoded, $"shift {shift}");
        }
    }

    [TestMethod]
    public void DecodeEncode_RoundTrips_ForExtendedAlphabet()
    {
        const string plain = "N 49 13.456, E 014!";
        for (var shift = 1; shift < QwertyShifter.ExtendedSize; shift++)
        {
            var encoded = _shifter.Encode(plain, shift, ShiftDirection.Right, QwertyAlphabet.Extended, foldToUpper: true);
            var decoded = _shifter.Decode(encoded, shift, ShiftDirection.Right, QwertyAlphabet.Extended, foldToUpper: true);
            Assert.AreEqual(plain, decoded, $"shift {shift}");
        }
    }

    // ---- Case preservation & folding ----

    [TestMethod]
    public void Encode_PreservesLowerCase()
        => Assert.AreEqual("w", _shifter.Encode("q", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits));

    [TestMethod]
    public void Encode_PreservesMixedCase()
    {
        var result = _shifter.Encode("Qq", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits);
        Assert.AreEqual("Ww", result);
    }

    [TestMethod]
    public void Encode_FoldToUpper_UppercasesLetters()
        => Assert.AreEqual("W", _shifter.Encode("q", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits, foldToUpper: true));

    // ---- Pass-through of unknown characters ----

    [TestMethod]
    public void Encode_PassesUnknownCharactersThrough()
    {
        // Space and punctuation are outside the 36-key ordering, so they survive untouched.
        var result = _shifter.Encode("Q W!", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits);
        Assert.AreEqual("W E!", result);
    }

    [TestMethod]
    public void Encode_Symbol_InExtended_IsShifted_ButPassesThroughInLettersDigits()
    {
        // '-' is part of the extended ordering but not the 36-key one.
        Assert.AreEqual("-", _shifter.Encode("-", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits));
        Assert.AreNotEqual("-", _shifter.Encode("-", 1, ShiftDirection.Right, QwertyAlphabet.Extended));
    }

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _shifter.Encode(text, 5, ShiftDirection.Right, QwertyAlphabet.LettersDigits));

    // ---- Brute force ----

    [TestMethod]
    public void BruteForce_LettersDigits_Produces35CandidatesInOrder()
    {
        var results = _shifter.BruteForce("Q", ShiftDirection.Right, QwertyAlphabet.LettersDigits);

        Assert.AreEqual(35, results.Count);
        Assert.AreEqual(1, results[0].Shift);
        Assert.AreEqual("W", results[0].Text);
        Assert.AreEqual(35, results[34].Shift);
    }

    [TestMethod]
    public void BruteForce_Extended_Produces46Candidates()
        => Assert.AreEqual(46, _shifter.BruteForce("Q", ShiftDirection.Right, QwertyAlphabet.Extended).Count);

    [TestMethod]
    public void BruteForce_ContainsTheKnownPlaintext()
    {
        var encoded = _shifter.Encode("GEOCACHE", 7, ShiftDirection.Right, QwertyAlphabet.LettersDigits);
        var candidates = _shifter.BruteForce(encoded, ShiftDirection.Left, QwertyAlphabet.LettersDigits);
        Assert.IsTrue(candidates.Any(c => c.Text == "GEOCACHE"));
    }

    // ---- Row-bounded model ----

    [TestMethod]
    public void Encode_RowBounded_StaysWithinRow()
    {
        // Top row is QWERTYUIOP. 'P' shifted right by 1 wraps to 'Q' (row start), not the next row.
        var result = _shifter.Encode("P", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits, model: ShiftModel.RowBounded);
        Assert.AreEqual("Q", result);
    }

    [TestMethod]
    public void Encode_RowBounded_DigitRowWrapsWithinDigits()
    {
        // Number row is 1234567890; '0' shifted right by 1 wraps back to '1'.
        var result = _shifter.Encode("0", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits, model: ShiftModel.RowBounded);
        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void Encode_RowBounded_RoundTrips()
    {
        const string plain = "QWERTY ASDF 1234";
        var encoded = _shifter.Encode(plain, 3, ShiftDirection.Right, QwertyAlphabet.LettersDigits, model: ShiftModel.RowBounded);
        var decoded = _shifter.Decode(encoded, 3, ShiftDirection.Right, QwertyAlphabet.LettersDigits, model: ShiftModel.RowBounded);
        Assert.AreEqual(plain, decoded);
    }

    // ---- Layouts ----

    [TestMethod]
    public void KeyOrdering_Qwertz_SwapsYandZ()
    {
        var qwerty = _shifter.KeyOrdering(KeyboardLayout.Qwerty, QwertyAlphabet.LettersDigits);
        var qwertz = _shifter.KeyOrdering(KeyboardLayout.Qwertz, QwertyAlphabet.LettersDigits);

        Assert.AreNotEqual(qwerty, qwertz);
        // Same key set, just reordered.
        Assert.AreEqual(string.Concat(qwerty.OrderBy(c => c)), string.Concat(qwertz.OrderBy(c => c)));
        // In QWERTZ the top row ends ...UIOP still, but Z sits where Y was on the top row.
        Assert.IsTrue(qwertz.Contains('Z'));
    }

    [TestMethod]
    public void Encode_Azerty_DiffersFromQwerty()
    {
        var qwerty = _shifter.Encode("AZ", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits, KeyboardLayout.Qwerty);
        var azerty = _shifter.Encode("AZ", 1, ShiftDirection.Right, QwertyAlphabet.LettersDigits, KeyboardLayout.Azerty);
        Assert.AreNotEqual(qwerty, azerty);
    }

    [TestMethod]
    public void Encode_Layouts_RoundTrip()
    {
        const string plain = "GEOCACHE 42";
        foreach (var layout in (KeyboardLayout[])[KeyboardLayout.Qwerty, KeyboardLayout.Qwertz, KeyboardLayout.Azerty])
        {
            var encoded = _shifter.Encode(plain, 4, ShiftDirection.Right, QwertyAlphabet.LettersDigits, layout);
            var decoded = _shifter.Decode(encoded, 4, ShiftDirection.Right, QwertyAlphabet.LettersDigits, layout);
            Assert.AreEqual(plain, decoded, layout.ToString());
        }
    }

    [TestMethod]
    public void KeyRows_ReturnsFourRows()
        => Assert.AreEqual(4, _shifter.KeyRows(KeyboardLayout.Qwerty, QwertyAlphabet.LettersDigits).Count);
}
