using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class SubstitutionCipherTests
{
    private readonly SubstitutionCipher _cipher = new();

    // dCode's worked example: with the substitution alphabet BYKWASLFXOCZTDHJUMIGPVENQR,
    // "DCODE" encrypts to "WKHWA". This is the canonical correctness anchor.
    private const string SampleKey = "BYKWASLFXOCZTDHJUMIGPVENQR";

    // ---- Encode / decode ----

    [TestMethod]
    public void Transform_Encode_MapsThroughTheKey()
        => Assert.AreEqual("WKHWA", _cipher.Transform("DCODE", SampleKey, decrypt: false));

    [TestMethod]
    public void Transform_Decode_AppliesTheInverse()
        => Assert.AreEqual("DCODE", _cipher.Transform("WKHWA", SampleKey, decrypt: true));

    [TestMethod]
    public void Transform_EncodeThenDecode_RoundTrips()
    {
        const string plain = "THEQUICKBROWNFOX";
        var encoded = _cipher.Transform(plain, SampleKey, decrypt: false);
        Assert.AreEqual(plain, _cipher.Transform(encoded, SampleKey, decrypt: true));
    }

    [TestMethod]
    public void Transform_FirstLetterMapsToKeyFirstLetter()
        => Assert.AreEqual("B", _cipher.Transform("A", SampleKey, decrypt: false));

    // ---- Case preservation (case-insensitive default) ----

    [TestMethod]
    public void Transform_PreservesCase_WhenNotCaseSensitive()
        => Assert.AreEqual("Wkhwa", _cipher.Transform("Dcode", SampleKey, decrypt: false, caseSensitive: false));

    [TestMethod]
    public void Transform_LowercaseInput_MapsViaKey()
        => Assert.AreEqual("wkhwa", _cipher.Transform("dcode", SampleKey, decrypt: false, caseSensitive: false));

    // ---- Case sensitive: a 26-letter key treats only its own case; the other case is "unknown" ----

    [TestMethod]
    public void Transform_CaseSensitive_LowercaseIsUnknownForUppercaseKey()
    {
        // Key is upper-case A-Z; in case-sensitive mode lower-case letters are NOT in the key.
        var result = _cipher.Transform("abc", SampleKey, decrypt: false,
            caseSensitive: true, unknownHandling: UnknownCharacterHandling.Replace);
        Assert.AreEqual("***", result);
    }

    [TestMethod]
    public void Transform_CaseSensitive_UppercaseStillMaps()
        => Assert.AreEqual("WKHWA", _cipher.Transform("DCODE", SampleKey, decrypt: false, caseSensitive: true));

    // ---- Unknown-character handling ----

    [TestMethod]
    public void Transform_Preserve_LeavesUnknownCharsUntouched()
        => Assert.AreEqual("WKHWA 13!", _cipher.Transform("DCODE 13!", SampleKey, decrypt: false,
            caseSensitive: false, unknownHandling: UnknownCharacterHandling.Preserve));

    [TestMethod]
    public void Transform_Remove_StripsUnknownChars()
        => Assert.AreEqual("WKHWA", _cipher.Transform("DCODE 13!", SampleKey, decrypt: false,
            caseSensitive: false, unknownHandling: UnknownCharacterHandling.Remove));

    [TestMethod]
    public void Transform_Replace_ReplacesUnknownCharsWithAsterisk()
        => Assert.AreEqual("WKHWA****", _cipher.Transform("DCODE 13!", SampleKey, decrypt: false,
            caseSensitive: false, unknownHandling: UnknownCharacterHandling.Replace));

    [TestMethod]
    public void Transform_DefaultHandling_IsPreserve()
        => Assert.AreEqual("WKHWA 1!", _cipher.Transform("DCODE 1!", SampleKey, decrypt: false));

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text, SampleKey, decrypt: false));

    // ---- InvertKey ----

    [TestMethod]
    public void InvertKey_ProducesTheReciprocalAlphabet()
    {
        // Applying the inverse key as a forward encode equals decoding with the original key.
        var inverse = _cipher.InvertKey(SampleKey);
        Assert.AreEqual("DCODE", _cipher.Transform("WKHWA", inverse, decrypt: false));
    }

    [TestMethod]
    public void InvertKey_OfIdentity_IsIdentity()
        => Assert.AreEqual(SubstitutionCipher.PlainAlphabet, _cipher.InvertKey(SubstitutionCipher.PlainAlphabet));

    [TestMethod]
    public void InvertKey_TwiceIsTheOriginal()
        => Assert.AreEqual(SampleKey, _cipher.InvertKey(_cipher.InvertKey(SampleKey)));

    // ---- Validation ----

    [TestMethod]
    public void Validate_ValidKey_IsValid()
    {
        var result = _cipher.Validate(SampleKey);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(KeyValidationError.None, result.Error);
    }

    [TestMethod]
    public void Validate_IgnoresCaseAndWhitespace()
    {
        var result = _cipher.Validate("bykwaslfxocztdhjumigpvenqr");
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_WrongLength_ReportsLengthError()
    {
        var result = _cipher.Validate("ABC");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KeyValidationError.WrongLength, result.Error);
    }

    [TestMethod]
    public void Validate_DuplicateLetter_ReportsDuplicate()
    {
        // 26 chars but 'A' twice and 'Z' missing.
        var result = _cipher.Validate("ABCDEFGHIJKLMNOPQRSTUVWXYA");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KeyValidationError.DuplicateLetter, result.Error);
        Assert.AreEqual('A', result.OffendingCharacter);
    }

    [TestMethod]
    public void Validate_NonLetter_ReportsNonLetter()
    {
        var result = _cipher.Validate("ABCDEFGHIJKLMNOPQRSTUVWXY1");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KeyValidationError.NonLetter, result.Error);
        Assert.AreEqual('1', result.OffendingCharacter);
    }

    [TestMethod]
    public void Validate_EmptyKey_ReportsWrongLength()
    {
        var result = _cipher.Validate("");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KeyValidationError.WrongLength, result.Error);
    }

    // ---- Keyword -> alphabet generation (dCode K1) ----

    [TestMethod]
    public void GenerateFromKeyword_DedupesThenAppendsRemaining()
    {
        // "CODE" -> C O D E, then the remaining A-Z in order skipping used letters.
        Assert.AreEqual("CODEABFGHIJKLMNPQRSTUVWXYZ", _cipher.GenerateFromKeyword("CODE"));
    }

    [TestMethod]
    public void GenerateFromKeyword_RepeatedLettersInKeywordAreDeduped()
        => Assert.AreEqual("KEYWORDABCFGHIJLMNPQSTUVXZ", _cipher.GenerateFromKeyword("KEYWORD"));

    [TestMethod]
    public void GenerateFromKeyword_IgnoresNonLettersAndCase()
        => Assert.AreEqual("CODEABFGHIJKLMNPQRSTUVWXYZ", _cipher.GenerateFromKeyword("c0o!d e."));

    [TestMethod]
    public void GenerateFromKeyword_EmptyKeyword_ReturnsPlainAlphabet()
        => Assert.AreEqual(SubstitutionCipher.PlainAlphabet, _cipher.GenerateFromKeyword(""));

    [TestMethod]
    public void GenerateFromKeyword_ProducesAValidKey()
        => Assert.IsTrue(_cipher.Validate(_cipher.GenerateFromKeyword("GEOCACHE")).IsValid);

    // ---- Auto-complete a partial key (beyond parity) ----

    [TestMethod]
    public void AutoComplete_AppendsRemainingLettersInOrder()
        => Assert.AreEqual("BYKACDEFGHIJLMNOPQRSTUVWXZ", _cipher.AutoComplete("BYK"));

    [TestMethod]
    public void AutoComplete_FillsUnusedLettersAlphabetically()
        => Assert.AreEqual("CODEABFGHIJKLMNPQRSTUVWXYZ", _cipher.AutoComplete("CODE"));

    [TestMethod]
    public void AutoComplete_FullKey_IsUnchanged()
        => Assert.AreEqual(SampleKey, _cipher.AutoComplete(SampleKey));

    [TestMethod]
    public void AutoComplete_IgnoresDuplicatesAndNonLetters()
        => Assert.AreEqual(SubstitutionCipher.PlainAlphabet, _cipher.AutoComplete("AABB12"));
}
