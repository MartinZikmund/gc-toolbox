using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class BrainfuckOokTests
{
    private readonly BrainfuckOok _codec = new();

    // Canonical Hello-World Brainfuck program.
    private const string HelloWorld =
        "++++++++[>++++[>++>+++>+++>+<<<<-]>+>+>->>+[<]<-]>>.>---.+++++++..+++.>>.<-.<.+++.------.--------.>>+.>++.";

    // ---- Execute: code -> text ----

    [TestMethod]
    public void Execute_HelloWorldProgram_PrintsHelloWorld()
    {
        var result = _codec.Execute(HelloWorld, string.Empty, BrainfuckNotation.Brainfuck);
        Assert.AreEqual("Hello World!\n", result.Output);
        Assert.IsFalse(result.Aborted);
    }

    [TestMethod]
    public void Execute_NonCommandCharacters_AreIgnored()
    {
        // The same program peppered with comment words and whitespace (no stray BF commands).
        var commented = string.Concat(HelloWorld.Select((c, i) => i % 5 == 0 ? $" wxyz\n{c}" : c.ToString()));
        var clean = _codec.Execute(HelloWorld, string.Empty, BrainfuckNotation.Brainfuck);
        var withComments = _codec.Execute(commented, string.Empty, BrainfuckNotation.Brainfuck);
        Assert.AreEqual(clean.Output, withComments.Output);
    }

    [TestMethod]
    public void Execute_CommaCommand_ReadsFromInput()
    {
        // ",." reads one byte from input and prints it; ",.,." echoes two bytes.
        var result = _codec.Execute(",.,.", "Hi", BrainfuckNotation.Brainfuck);
        Assert.AreEqual("Hi", result.Output);
    }

    [TestMethod]
    public void Execute_CommaPastEndOfInput_YieldsZeroByte()
    {
        // Reading past the end leaves the cell at 0; printing 0 emits the NUL char.
        var result = _codec.Execute(",.", string.Empty, BrainfuckNotation.Brainfuck);
        Assert.AreEqual("\0", result.Output);
    }

    [TestMethod]
    public void Execute_PlusMinusWrapAroundCell_IsModulo256()
    {
        // 256 increments wrap a byte cell back to 0, so '.' prints NUL.
        var program = new string('+', 256) + ".";
        var result = _codec.Execute(program, string.Empty, BrainfuckNotation.Brainfuck);
        Assert.AreEqual("\0", result.Output);
    }

    [TestMethod]
    public void Execute_NonTerminatingProgram_IsAbortedByStepGuard()
    {
        // "+[]" loops forever (cell never reaches 0). The guard must stop it.
        var result = _codec.Execute("+[]", string.Empty, BrainfuckNotation.Brainfuck);
        Assert.IsTrue(result.Aborted);
    }

    [TestMethod]
    public void Execute_RunawayOutput_IsAbortedByOutputGuard()
    {
        // "+[.]" prints endlessly. The output cap must stop it without hanging.
        var result = _codec.Execute("+[.]", string.Empty, BrainfuckNotation.Brainfuck);
        Assert.IsTrue(result.Aborted);
    }

    [TestMethod]
    public void Execute_UnbalancedBrackets_AreReportedAsError()
    {
        var result = _codec.Execute("+[+", string.Empty, BrainfuckNotation.Brainfuck);
        Assert.IsTrue(result.HasError);
    }

    // ---- Text -> Brainfuck (round-trip) ----

    [DataTestMethod]
    [DataRow("GEO")]
    [DataRow("N 50 06.123")]
    [DataRow("Hello World!")]
    [DataRow("")]
    public void EncodeText_ThenExecute_RoundTrips(string text)
    {
        var program = _codec.EncodeText(text, BrainfuckNotation.Brainfuck);
        var result = _codec.Execute(program, string.Empty, BrainfuckNotation.Brainfuck);
        Assert.AreEqual(text, result.Output);
        Assert.IsFalse(result.Aborted);
    }

    [TestMethod]
    public void EncodeText_OokNotation_RoundTrips()
    {
        var program = _codec.EncodeText("GEO", BrainfuckNotation.Ook);
        var result = _codec.Execute(program, string.Empty, BrainfuckNotation.Ook);
        Assert.AreEqual("GEO", result.Output);
    }

    [TestMethod]
    public void EncodeText_OokShortNotation_RoundTrips()
    {
        var program = _codec.EncodeText("GEO", BrainfuckNotation.OokShort);
        var result = _codec.Execute(program, string.Empty, BrainfuckNotation.OokShort);
        Assert.AreEqual("GEO", result.Output);
    }

    // ---- Exact command <-> Ook! token mapping ----

    [DataTestMethod]
    [DataRow('>', "Ook. Ook?")]
    [DataRow('<', "Ook? Ook.")]
    [DataRow('+', "Ook. Ook.")]
    [DataRow('-', "Ook! Ook!")]
    [DataRow('.', "Ook! Ook.")]
    [DataRow(',', "Ook. Ook!")]
    [DataRow('[', "Ook! Ook?")]
    [DataRow(']', "Ook? Ook!")]
    public void ToOok_SingleCommand_UsesExactTokenMapping(char command, string expected)
        => Assert.AreEqual(expected, _codec.Transliterate(command.ToString(), BrainfuckNotation.Brainfuck, BrainfuckNotation.Ook));

    [DataTestMethod]
    [DataRow('>', ".?")]
    [DataRow('<', "?.")]
    [DataRow('+', "..")]
    [DataRow('-', "!!")]
    [DataRow('.', "!.")]
    [DataRow(',', ".!")]
    [DataRow('[', "!?")]
    [DataRow(']', "?!")]
    public void ToOokShort_SingleCommand_UsesExactTokenMapping(char command, string expected)
        => Assert.AreEqual(expected, _codec.Transliterate(command.ToString(), BrainfuckNotation.Brainfuck, BrainfuckNotation.OokShort));

    // ---- Cross-notation transliteration in every direction ----

    [TestMethod]
    public void Transliterate_BrainfuckToOokAndBack_RoundTrips()
    {
        var ook = _codec.Transliterate(HelloWorld, BrainfuckNotation.Brainfuck, BrainfuckNotation.Ook);
        var bf = _codec.Transliterate(ook, BrainfuckNotation.Ook, BrainfuckNotation.Brainfuck);
        Assert.AreEqual(HelloWorld, bf);
    }

    [TestMethod]
    public void Transliterate_BrainfuckToOokShortAndBack_RoundTrips()
    {
        var shortOok = _codec.Transliterate(HelloWorld, BrainfuckNotation.Brainfuck, BrainfuckNotation.OokShort);
        var bf = _codec.Transliterate(shortOok, BrainfuckNotation.OokShort, BrainfuckNotation.Brainfuck);
        Assert.AreEqual(HelloWorld, bf);
    }

    [TestMethod]
    public void Transliterate_OokToOokShortAndBack_RoundTrips()
    {
        var ook = _codec.Transliterate(HelloWorld, BrainfuckNotation.Brainfuck, BrainfuckNotation.Ook);
        var shortOok = _codec.Transliterate(ook, BrainfuckNotation.Ook, BrainfuckNotation.OokShort);
        var ook2 = _codec.Transliterate(shortOok, BrainfuckNotation.OokShort, BrainfuckNotation.Ook);
        Assert.AreEqual(ook, ook2);
    }

    [TestMethod]
    public void Transliterate_MalformedOokInput_ThrowsFormatException()
    {
        // An odd number of Ook! symbols can't pair into commands — the codec reports it as a FormatException
        // (which the Convert-mode view model branch catches and surfaces, rather than crashing).
        Assert.ThrowsExactly<FormatException>(
            () => _codec.Transliterate("Ook. Ook? Ook.", BrainfuckNotation.Ook, BrainfuckNotation.Brainfuck));
    }

    [TestMethod]
    public void Transliterate_OokIsWhitespaceTolerant()
    {
        // Newlines and extra spaces between tokens must still parse.
        var messy = "Ook.\tOok?\n  Ook.   Ook.";
        var bf = _codec.Transliterate(messy, BrainfuckNotation.Ook, BrainfuckNotation.Brainfuck);
        Assert.AreEqual(">+", bf);
    }

    [TestMethod]
    public void ExecuteOok_HelloWorld_PrintsHelloWorld()
    {
        var ook = _codec.Transliterate(HelloWorld, BrainfuckNotation.Brainfuck, BrainfuckNotation.Ook);
        var result = _codec.Execute(ook, string.Empty, BrainfuckNotation.Ook);
        Assert.AreEqual("Hello World!\n", result.Output);
    }

    // ---- Auto-detect notation ----

    [TestMethod]
    public void Detect_BrainfuckProgram_ReturnsBrainfuck()
        => Assert.AreEqual(BrainfuckNotation.Brainfuck, _codec.Detect(HelloWorld));

    [TestMethod]
    public void Detect_OokProgram_ReturnsOok()
    {
        var ook = _codec.Transliterate(HelloWorld, BrainfuckNotation.Brainfuck, BrainfuckNotation.Ook);
        Assert.AreEqual(BrainfuckNotation.Ook, _codec.Detect(ook));
    }

    [TestMethod]
    public void Detect_OokShortProgram_ReturnsOokShort()
    {
        var shortOok = _codec.Transliterate(HelloWorld, BrainfuckNotation.Brainfuck, BrainfuckNotation.OokShort);
        Assert.AreEqual(BrainfuckNotation.OokShort, _codec.Detect(shortOok));
    }
}
