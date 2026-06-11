using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

/// <summary>
/// The interactive multitap composer is a pure state machine: a "fast" re-tap of the same key arrives
/// while a letter is still pending and cycles it; a "slow" tap arrives after the View's timer has
/// already called <see cref="PhoneKeypadComposer.CommitPending"/>, so it starts a fresh letter. These
/// tests drive that timing explicitly by interleaving <c>Press</c> and <c>CommitPending</c>.
/// </summary>
[TestClass]
public class PhoneKeypadComposerTests
{
    [TestMethod]
    public void Press_TwiceSameKey_CyclesToSecondLetter()
    {
        var c = new PhoneKeypadComposer();
        c.Press('2');
        c.Press('2');

        Assert.AreEqual('B', c.PendingChar);
        Assert.AreEqual("B", c.DisplayText);
        Assert.AreEqual("22", c.PressCode);
    }

    [TestMethod]
    public void Press_FourTimes_WrapsAroundThreeLetterKey()
    {
        var c = new PhoneKeypadComposer();
        for (var i = 0; i < 4; i++)
        {
            c.Press('2');
        }

        Assert.AreEqual('A', c.PendingChar); // ABC has 3 letters; 4th press wraps to A
    }

    [TestMethod]
    public void CommitPending_MovesPendingIntoCommittedText()
    {
        var c = new PhoneKeypadComposer();
        c.Press('2');
        c.Press('2'); // B pending
        c.CommitPending();

        Assert.AreEqual("B", c.CommittedText);
        Assert.IsNull(c.PendingChar);
    }

    [TestMethod]
    public void Press_DifferentKey_CommitsPreviousPendingLetter()
    {
        var c = new PhoneKeypadComposer();
        c.Press('4');
        c.Press('4');  // H pending
        c.Press('3');  // commits H, starts D
        c.Press('3');  // E pending

        Assert.AreEqual("HE", c.DisplayText);
    }

    [TestMethod]
    public void Composes_Hello_WithPausesBetweenSameKeyLetters()
    {
        var c = new PhoneKeypadComposer();
        TapLetter(c, '4', 2);  // H
        TapLetter(c, '3', 2);  // E
        TapLetter(c, '5', 3);  // L
        TapLetter(c, '5', 3);  // L — the pause (CommitPending) is what separates the two L's
        TapLetter(c, '6', 3);  // O

        Assert.AreEqual("HELLO", c.CommittedText);
        Assert.AreEqual("44 33 555 555 666", c.PressCode);
    }

    [TestMethod]
    public void Press_ZeroKey_InsertsSpaceImmediately()
    {
        var c = new PhoneKeypadComposer();
        Tap(c, '4', 2); // H
        c.Press('0');   // space commits immediately (single-character key, no timeout needed)

        Assert.AreEqual("H ", c.CommittedText);
        Assert.IsNull(c.PendingChar);
    }

    [TestMethod]
    public void Backspace_CancelsPendingLetterFirst()
    {
        var c = new PhoneKeypadComposer();
        c.Press('2'); // A pending
        c.Backspace();

        Assert.AreEqual(string.Empty, c.DisplayText);
        Assert.IsNull(c.PendingChar);
    }

    [TestMethod]
    public void Backspace_WithNoPending_RemovesLastCommittedCharacterAndItsCode()
    {
        var c = new PhoneKeypadComposer();
        TapLetter(c, '2', 1); // A
        TapLetter(c, '2', 2); // B
        c.Backspace();

        Assert.AreEqual("A", c.CommittedText);
        Assert.AreEqual("2", c.PressCode);
    }

    [TestMethod]
    public void Clear_ResetsEverything()
    {
        var c = new PhoneKeypadComposer();
        Tap(c, '2', 2);
        c.CommitPending();
        c.Clear();

        Assert.AreEqual(string.Empty, c.DisplayText);
        Assert.AreEqual(string.Empty, c.PressCode);
        Assert.IsNull(c.PendingChar);
    }

    [TestMethod]
    public void Press_UnknownKey_IsIgnored()
    {
        var c = new PhoneKeypadComposer();
        c.Press('A'); // not a keypad digit

        Assert.AreEqual(string.Empty, c.DisplayText);
    }

    [TestMethod]
    public void DisplayText_IncludesPendingLetterBeforeCommit()
    {
        var c = new PhoneKeypadComposer();
        Tap(c, '8', 2); // U pending (TUV, second letter) — not yet committed

        Assert.AreEqual("U", c.DisplayText);
        Assert.AreEqual(string.Empty, c.CommittedText);
        Assert.AreEqual('U', c.PendingChar);
    }

    /// <summary>Presses <paramref name="key"/> <paramref name="times"/> times as one fast multitap burst
    /// (leaves the letter pending — the View's timer hasn't fired yet).</summary>
    private static void Tap(PhoneKeypadComposer c, char key, int times)
    {
        for (var i = 0; i < times; i++)
        {
            c.Press(key);
        }
    }

    /// <summary>Types one whole letter: a fast burst of <paramref name="times"/> presses, then the pause
    /// (<see cref="PhoneKeypadComposer.CommitPending"/>) the View's timeout would supply.</summary>
    private static void TapLetter(PhoneKeypadComposer c, char key, int times)
    {
        Tap(c, key, times);
        c.CommitPending();
    }
}
