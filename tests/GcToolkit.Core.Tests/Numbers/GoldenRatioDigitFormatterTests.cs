using GcToolkit.Core.Numbers.GoldenRatio;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public sealed class GoldenRatioDigitFormatterTests
{
    private const string First25 = "6180339887498948482045868";
    private const string First50 = "61803398874989484820458683436563811772030917980576";
    private const string First60 = "618033988749894848204586834365638117720309179805762862135448";

    [TestMethod]
    public void Format_GroupedNoLineNumbers_ProducesBlocksOf10()
    {
        var formatted = GoldenRatioDigitFormatter.Format(First25, startPosition: 1, groupBlocks: true, lineNumbers: false);

        Assert.AreEqual("6180339887 4989484820 45868", formatted);
    }

    [TestMethod]
    public void Format_UngroupedNoLineNumbers_ReturnsRawDigits()
    {
        var formatted = GoldenRatioDigitFormatter.Format(First25, startPosition: 1, groupBlocks: false, lineNumbers: false);

        Assert.AreEqual(First25, formatted);
    }

    [TestMethod]
    public void Format_GroupedWithLineNumbers_LabelsEachLineWithLastPosition()
    {
        var formatted = GoldenRatioDigitFormatter.Format(First60, startPosition: 1, groupBlocks: true, lineNumbers: true);

        var expected =
            "6180339887 4989484820 4586834365 6381177203 0917980576  (50)\n" +
            "2862135448  (60)";
        Assert.AreEqual(expected, formatted);
    }

    [TestMethod]
    public void Format_LineNumbersWithStartOffset_LabelsUseAbsolutePositions()
    {
        var formatted = GoldenRatioDigitFormatter.Format("4989484820", startPosition: 11, groupBlocks: true, lineNumbers: true);

        Assert.AreEqual("4989484820  (20)", formatted);
    }

    [TestMethod]
    public void Format_ExactLineMultiple_HasNoTrailingLine()
    {
        var formatted = GoldenRatioDigitFormatter.Format(First50, startPosition: 1, groupBlocks: false, lineNumbers: true);

        Assert.AreEqual(First50 + "  (50)", formatted);
    }

    [TestMethod]
    public void Format_EmptyInput_ReturnsEmpty()
    {
        var formatted = GoldenRatioDigitFormatter.Format(string.Empty, startPosition: 1, groupBlocks: true, lineNumbers: true);

        Assert.AreEqual(string.Empty, formatted);
    }
}
