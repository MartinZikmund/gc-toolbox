using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class GcCodeIdConverterTests
{
    private readonly GcCodeIdConverter _converter = new();

    // ---- Verified anchors: code -> id ----

    [DataTestMethod]
    [DataRow("GC1", 1L)]
    [DataRow("GCFFFF", 65535L)]
    [DataRow("GCG000", 65536L)]
    [DataRow("GC16XYD", 718967L)]
    public void GcCodeToId_VerifiedAnchors_ReturnsExpectedId(string code, long expected)
        => Assert.AreEqual(expected, _converter.GcCodeToId(code));

    // ---- Verified anchors: id -> code ----

    [DataTestMethod]
    [DataRow(1L, "GC1")]
    [DataRow(65535L, "GCFFFF")]
    [DataRow(65536L, "GCG000")]
    [DataRow(718967L, "GC16XYD")]
    public void IdToGcCode_VerifiedAnchors_ReturnsExpectedCode(long id, string expected)
        => Assert.AreEqual(expected, _converter.IdToGcCode(id));

    // ---- The boundary between the legacy hex era and the modern base-31 era ----

    [TestMethod]
    public void GcCodeToId_LastHexAndFirstBase31_StraddleTheBoundary()
    {
        Assert.AreEqual(65535L, _converter.GcCodeToId("GCFFFF"));
        Assert.AreEqual(65536L, _converter.GcCodeToId("GCG000"));
    }

    [TestMethod]
    public void IdToGcCode_LastHexAndFirstBase31_StraddleTheBoundary()
    {
        Assert.AreEqual("GCFFFF", _converter.IdToGcCode(65535L));
        Assert.AreEqual("GCG000", _converter.IdToGcCode(65536L));
    }

    // ---- The documented worked example ----

    [TestMethod]
    public void GcCodeToId_WorkedExampleGc16Xyd_Returns718967()
        => Assert.AreEqual(718967L, _converter.GcCodeToId("GC16XYD"));

    // ---- Round-trips across both eras ----

    [DataTestMethod]
    [DataRow("GC1")]
    [DataRow("GCFFFF")]
    [DataRow("GCG000")]
    [DataRow("GC16XYD")]
    [DataRow("GCZZZZZ")]
    [DataRow("GC12345")]
    [DataRow("GCABCDE")]
    public void RoundTrip_CodeToIdToCode_PreservesCode(string code)
    {
        var id = _converter.GcCodeToId(code);
        Assert.IsNotNull(id);
        Assert.AreEqual(code, _converter.IdToGcCode(id.Value));
    }

    [DataTestMethod]
    [DataRow(1L)]
    [DataRow(100L)]
    [DataRow(65535L)]
    [DataRow(65536L)]
    [DataRow(718967L)]
    [DataRow(28218030L)]
    public void RoundTrip_IdToCodeToId_PreservesId(long id)
    {
        var code = _converter.IdToGcCode(id);
        Assert.IsNotNull(code);
        Assert.AreEqual(id, _converter.GcCodeToId(code));
    }

    // ---- Ambiguous characters (I, L, O, S, U) are not in the base-31 charset ----

    [DataTestMethod]
    [DataRow("GCI")]
    [DataRow("GCL")]
    [DataRow("GCO")]
    [DataRow("GCS")]
    [DataRow("GCU")]
    [DataRow("GC1234I")]
    public void GcCodeToId_AmbiguousChars_ReturnsNull(string code)
        => Assert.IsNull(_converter.GcCodeToId(code));

    // ---- Sub-threshold modern bodies are not real codes (must reject, not return a bogus id) ----

    [DataTestMethod]
    [DataRow("GCG")]
    [DataRow("GCX")]
    [DataRow("GCG00")]
    [DataRow("GC0G00")]
    [DataRow("GCFZZZ")]
    public void GcCodeToId_SubThresholdModernBody_ReturnsNull(string code)
        => Assert.IsNull(_converter.GcCodeToId(code));

    [TestMethod]
    public void GcCodeToId_FirstModernCodeAndLastHex_AreUnaffectedByTheGuard()
    {
        Assert.AreEqual(65536L, _converter.GcCodeToId("GCG000"));
        Assert.AreEqual(65535L, _converter.GcCodeToId("GCFFFF"));
    }

    // ---- The "GC" prefix is optional ----

    [TestMethod]
    public void GcCodeToId_WithoutPrefix_IsTreatedTheSame()
    {
        Assert.AreEqual(718967L, _converter.GcCodeToId("16XYD"));
        Assert.AreEqual(_converter.GcCodeToId("GC16XYD"), _converter.GcCodeToId("16XYD"));
    }

    // ---- Case-insensitive ----

    [TestMethod]
    public void GcCodeToId_LowercaseInput_IsUppercased()
        => Assert.AreEqual(718967L, _converter.GcCodeToId("gc16xyd"));

    // ---- Whitespace is trimmed ----

    [TestMethod]
    public void GcCodeToId_LeadingAndTrailingWhitespace_IsTrimmed()
        => Assert.AreEqual(718967L, _converter.GcCodeToId("  GC16XYD  "));

    // ---- Empty / null input ----

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("GC")]
    public void GcCodeToId_EmptyOrPrefixOnly_ReturnsNull(string code)
        => Assert.IsNull(_converter.GcCodeToId(code));

    [TestMethod]
    public void GcCodeToId_Null_ReturnsNull()
        => Assert.IsNull(_converter.GcCodeToId(null));

    // ---- Negative ids have no code ----

    [TestMethod]
    public void IdToGcCode_NegativeId_ReturnsNull()
        => Assert.IsNull(_converter.IdToGcCode(-1));

    // ---- Non-numeric / invalid ids are rejected by the parser used at the VM layer ----

    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("12.5")]
    [DataRow("")]
    public void TryParseId_NonNumeric_ReturnsFalse(string text)
        => Assert.IsFalse(GcCodeIdConverter.TryParseId(text, out _));

    [TestMethod]
    public void TryParseId_PlainNumber_ReturnsTrue()
    {
        Assert.IsTrue(GcCodeIdConverter.TryParseId("718967", out var id));
        Assert.AreEqual(718967L, id);
    }
}
