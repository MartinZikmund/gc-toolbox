using GcToolkit.Core.Numbers.Pi;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class PiDigitsFormatterTests
{
    private const string Twenty = "12345678901234567890";

    [TestMethod]
    public void Format_NoOptions_ReturnsRawDigits()
        => Assert.AreEqual(Twenty, PiDigitsFormatter.Format(Twenty, startPosition: 1, groupDigits: false, lineNumbers: false));

    [TestMethod]
    public void Format_Grouped_SplitsIntoBlocksOfTen()
        => Assert.AreEqual(
            "1234567890 1234567890",
            PiDigitsFormatter.Format(Twenty, startPosition: 1, groupDigits: true, lineNumbers: false));

    [TestMethod]
    public void Format_Grouped_WrapsAfterFiveBlocks()
    {
        var sixtyDigits = string.Concat(Enumerable.Repeat("0123456789", 6));

        var formatted = PiDigitsFormatter.Format(sixtyDigits, startPosition: 1, groupDigits: true, lineNumbers: false);
        var lines = formatted.Split('\n');

        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual(string.Join(" ", Enumerable.Repeat("0123456789", 5)), lines[0]);
        Assert.AreEqual("0123456789", lines[1]);
    }

    [TestMethod]
    public void Format_LineNumbers_PrefixesEachLineWithItsStartPosition()
    {
        var sixtyDigits = string.Concat(Enumerable.Repeat("0123456789", 6));

        var formatted = PiDigitsFormatter.Format(sixtyDigits, startPosition: 1, groupDigits: false, lineNumbers: true);
        var lines = formatted.Split('\n');

        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual(" 1: " + string.Concat(Enumerable.Repeat("0123456789", 5)), lines[0]);
        Assert.AreEqual("51: 0123456789", lines[1]);
    }

    [TestMethod]
    public void Format_LineNumbersWithCustomStart_UsesAbsolutePositions()
    {
        var formatted = PiDigitsFormatter.Format(Twenty, startPosition: 762, groupDigits: true, lineNumbers: true);

        Assert.AreEqual("762: 1234567890 1234567890", formatted);
    }

    [TestMethod]
    public void Format_EmptyDigits_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, PiDigitsFormatter.Format(string.Empty, startPosition: 1, groupDigits: true, lineNumbers: true));
}
