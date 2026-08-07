using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>
/// A ListView item with no explicit automation name announces its ToString(), so every candidate-row
/// type must render the row's content rather than its type name (or a record's generated form).
/// </summary>
[TestClass]
public class CandidateRowAutomationNameTests
{
    private static void NoOp(string _)
    {
    }

    [TestMethod]
    public void AffineCandidateItem_ToString_IsKeyAndText()
    {
        AffineCandidateItem item = new(1, 0, "MJQQI", NoOp);

        Assert.AreEqual("a=1, b=0: MJQQI", item.ToString());
    }

    [TestMethod]
    public void RailFenceSolveItem_ToString_IsLabelAndText()
    {
        RailFenceSolveItem item = new(3, 0, "HELLOWORLD", NoOp);

        Assert.AreEqual("3 / 0: HELLOWORLD", item.ToString());
    }

    [TestMethod]
    public void ScytaleSolveItem_ToString_IsColumnsAndText()
    {
        ScytaleSolveItem item = new(3, "HELPME", NoOp);

        Assert.AreEqual("3: HELPME", item.ToString());
    }

    [TestMethod]
    public void TrithemiusOffsetItem_ToString_IsOffsetAndText()
    {
        TrithemiusOffsetItem item = new(0, "HDJIK", NoOp);

        Assert.AreEqual("0: HDJIK", item.ToString());
    }

    [TestMethod]
    public void LucasNumberItem_ToString_IsIndexAndValue_NotTheRecordForm()
    {
        LucasNumberItem item = new(0, "2", 1);

        Assert.AreEqual("0: 2", item.ToString());
    }

    [TestMethod]
    public void WordValueWordItem_ToString_IsWordAndDetail_NotTheRecordForm()
    {
        WordValueWordItem item = new("cache", "3 + 1 + 3 + 8 + 5 = 20 = 2");

        Assert.AreEqual("cache: 3 + 1 + 3 + 8 + 5 = 20 = 2", item.ToString());
    }

    [TestMethod]
    public void WordValueConversionItem_ToString_IsCharacterAndValue_NotTheRecordForm()
    {
        WordValueConversionItem item = new("a", 1);

        Assert.AreEqual("a = 1", item.ToString());
    }

    [TestMethod]
    public void BaseResultItem_ToString_IsLabelAndValue()
    {
        BaseResultItem item = new("Base 2", "11111111", NoOp);

        Assert.AreEqual("Base 2: 11111111", item.ToString());
    }

    [TestMethod]
    public void BaseBatchItem_ToString_IsInputAndResult()
    {
        BaseBatchItem item = new("FF", "255", isValid: true, "skipped", NoOp);

        Assert.AreEqual("FF: 255", item.ToString());
    }

    [TestMethod]
    public void BaseBatchItem_ToString_SkippedToken_AnnouncesWhyItWasSkipped()
    {
        BaseBatchItem item = new("ZZ", string.Empty, isValid: false, "Skipped — not valid in base 16", NoOp);

        Assert.AreEqual("ZZ: Skipped — not valid in base 16", item.ToString());
    }

    [TestMethod]
    public void BaseOption_ToString_IsTheDisplayName_NotTheRecordForm()
    {
        BaseOption option = new(16, "Hexadecimal (16)");

        Assert.AreEqual("Hexadecimal (16)", option.ToString());
    }

    [TestMethod]
    public void VigenereKeyLengthItem_ToString_IsLengthAndScore()
    {
        VigenereKeyLengthItem item = new(5, 0.0661);

        // Built with the same (current) culture the row formats with, so a comma decimal separator passes.
        Assert.AreEqual($"5: IC {0.0661:0.0000}", item.ToString());
    }

    [TestMethod]
    public void VigenereKeyRow_ToString_IsKeyLetterAndCipherAlphabet()
    {
        VigenereKeyRow row = new('B', "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "BCDEFGHIJKLMNOPQRSTUVWXYZA");

        Assert.AreEqual("B: BCDEFGHIJKLMNOPQRSTUVWXYZA", row.ToString());
    }

    [TestMethod]
    public void GcCodeIdResultItem_ToString_IsInputAndOutput()
    {
        GcCodeIdResultItem item = new("GC16XYD", "718967", isValid: true, NoOp);

        Assert.AreEqual("GC16XYD: 718967", item.ToString());
    }
}
