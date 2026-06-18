using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class BookCipherViewModelTests
{
    private const string Book = "The quick brown fox\njumps over the lazy dog";

    private static BookCipherViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        var catalog = new StubCatalogService("BookCipher");
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["Name_BookCipher"] = "Book cipher",
            ["BookCipherDecodeErrorFormat"] = "{0} reference(s) failed; '{1}': {2}",
            ["BookCipherEncodeErrorFormat"] = "{0} word(s) not found; '{1}': {2}",
            ["BookCipherNoPartsWarning"] = "Configure at least one reference part.",
        });

        var vm = new BookCipherViewModel(
            catalog,
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            localizer,
            clipboard,
            share);

        vm.ViewCreated(); // activate so ToolName/recents populate
        return vm;
    }

    [TestMethod]
    public void Decode_WordMode_ProducesLiveResult()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;

        vm.CodesText = "1 4 9";

        Assert.AreEqual("The fox dog", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void Decode_UpdatesLiveWhenFormatChanges()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.CodesText = "1:3";

        // word-only by default => "1:3" is two numbers for one part => error
        Assert.IsTrue(vm.HasWarning);

        // switch to line:word and it resolves
        vm.Part1Index = (int)BookReferencePart.Line;
        vm.Part2Index = (int)BookReferencePart.Word;

        Assert.AreEqual("brown", vm.OutputText);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void Decode_NumberingBaseToggle_ShiftsResult()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.CodesText = "2";

        Assert.AreEqual("quick", vm.OutputText); // 1-based default

        vm.NumberingBaseIndex = 1; // 0-based
        Assert.AreEqual("brown", vm.OutputText); // index 2 0-based = 3rd word
    }

    [TestMethod]
    public void Decode_FirstLetterMode_PullsFirstLetters()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.ExtractionIndex = (int)BookCipherExtraction.FirstLetter;
        vm.IgnoreSpaces = true;

        vm.CodesText = "1 2 3";

        Assert.AreEqual("Tqb", vm.OutputText);
    }

    [TestMethod]
    public void Extraction_NthLetter_ShowsLetterIndexField()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.IsFalse(vm.ShowLetterIndex);

        vm.ExtractionIndex = (int)BookCipherExtraction.NthLetter;

        Assert.IsTrue(vm.ShowLetterIndex);
    }

    [TestMethod]
    public void Decode_OutOfRange_SetsWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;

        vm.CodesText = "999";

        Assert.IsTrue(vm.HasWarning);
        StringAssert.Contains(vm.WarningMessage, "out of range");
    }

    [TestMethod]
    public void EmptyCodes_ClearsOutputAndWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.CodesText = "1";
        Assert.IsTrue(vm.HasOutput);

        vm.CodesText = "   ";

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void Encode_GeneratesReferences()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.DirectionIndex = 1; // encode
        vm.CodesText = "fox dog";

        Assert.IsTrue(vm.IsEncoding);
        Assert.IsFalse(vm.HasWarning);
        // word indices: fox=4, dog=9
        Assert.AreEqual("4 9", vm.OutputText);
    }

    [TestMethod]
    public void Swap_CarriesResultIntoInputAndFlipsDirection()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.CodesText = "4 9";

        Assert.AreEqual("fox dog", vm.OutputText);

        vm.SwapDirectionCommand.Execute(null);

        Assert.IsTrue(vm.IsEncoding);
        Assert.AreEqual("fox dog", vm.CodesText);
        // re-encoding "fox dog" reproduces the references
        Assert.AreEqual("4 9", vm.OutputText);
    }

    [TestMethod]
    public void Copy_PutsResultOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.BookText = Book;
        vm.CodesText = "1";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("The", clipboard.LastText);
    }

    [TestMethod]
    public async Task Share_SendsResultToShareService()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.BookText = Book;
        vm.CodesText = "1";

        await vm.ShareOutputCommand.ExecuteAsync(null);

        Assert.IsNotNull(share.LastShared);
        Assert.AreEqual("The", share.LastShared!.Value.Text);
    }

    [TestMethod]
    public void Clear_EmptiesCodesAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.BookText = Book;
        vm.CodesText = "1";
        Assert.IsTrue(vm.HasOutput);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.CodesText);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    [TestMethod]
    public void CopyShare_DisabledWhenNoOutput()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(vm.ShareOutputCommand.CanExecute(null));

        vm.BookText = Book;
        vm.CodesText = "1";

        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsTrue(vm.ShareOutputCommand.CanExecute(null));
    }
}
