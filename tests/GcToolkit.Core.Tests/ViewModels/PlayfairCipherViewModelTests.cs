using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class PlayfairCipherViewModelTests
{
    private static PlayfairCipherViewModel CreateViewModel(out FakeClipboardService clipboard, out FakeShareService share)
    {
        CatalogService catalog = new(
            [new StubCategoryContributor(new Category("Ciphers", "Ciphers_Name", 0, "Ciphers"))],
            [new StubToolContributor(new ToolDescriptor(
                "PlayfairCipher", "PlayfairCipher_Name", "Ciphers", [], "PlayfairCipher", typeof(object), false, "PlayfairCipher_Tooltip"))],
            new ToolMatcher(),
            new FakeStringLocalizer());

        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "PlayfairCipher_Name", "Playfair cipher" },
            { "PlayfairStrippedNotice", "Stripped characters." },
            { "PlayfairErrorManualSquare", "Invalid manual square." },
            { "PlayfairErrorOddCiphertext", "Ciphertext must be even." },
        });

        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        PlayfairCipherViewModel vm = new(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Constructor_PopulatesTheLiveKeySquare()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.AreEqual(25, vm.SquareCells.Count);
        Assert.AreEqual("P", vm.SquareCells[0].Letter);
        Assert.AreEqual(1, vm.SquareCells[0].Row);
        Assert.AreEqual(1, vm.SquareCells[0].Column);
    }

    [TestMethod]
    public void Keyword_TypingRebuildsTheSquare()
    {
        var vm = CreateViewModel(out _, out _);

        vm.Keyword = "monarchy";

        Assert.AreEqual("M", vm.SquareCells[0].Letter);
        Assert.AreEqual("O", vm.SquareCells[1].Letter);
    }

    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesKnownCiphertext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "playfair example";

        vm.InputText = "hide the gold in the tree stump";

        Assert.AreEqual("BMODZBXDNABEKUDMUIXMMOUVIF", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void Decrypt_OfTheVector_RecoversThePaddedPlaintext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "playfair example";
        vm.DirectionIndex = 1;

        vm.InputText = "BMODZBXDNABEKUDMUIXMMOUVIF";

        Assert.AreEqual("HIDETHEGOLDINTHETREXESTUMP", vm.OutputText);
    }

    [TestMethod]
    public void GroupOutput_InsertsSpacesEveryFiveLetters()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "playfair example";
        vm.InputText = "hide the gold in the tree stump";

        vm.GroupOutput = true;

        Assert.AreEqual("BMODZ BXDNA BEKUD MUIXM MOUVI F", vm.OutputText);
    }

    [TestMethod]
    public void BatchMode_TransformsEachLineIndependently()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "playfair example";
        vm.BatchMode = true;

        vm.InputText = "hide\nthe gold";

        var lines = vm.OutputText.Split('\n', '\r');
        Assert.IsTrue(lines.Length >= 2);
        // First line "HIDE" → digraphs HI DE → "BMOD".
        Assert.AreEqual("BMOD", lines[0]);
    }

    [TestMethod]
    public void StrippedInput_RaisesTheStrippedNotice()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "playfair example";

        vm.InputText = "Hi, there! 123";

        Assert.IsTrue(vm.HasNotice);
        Assert.AreEqual("Stripped characters.", vm.NoticeMessage);
    }

    [TestMethod]
    public void EmptyInput_ClearsOutputAndNotice()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "hello";

        vm.InputText = "   ";

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasNotice);
    }

    [TestMethod]
    public void ManualSquare_Invalid_SurfacesTheManualSquareError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SquareSourceIndex = 1;
        vm.ManualSquare = "ABC"; // too short

        vm.InputText = "hello";

        Assert.IsTrue(vm.HasNotice);
        Assert.AreEqual("Invalid manual square.", vm.NoticeMessage);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void OddDecryptInput_SurfacesTheOddCiphertextError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1;

        vm.InputText = "ABC";

        Assert.IsTrue(vm.HasNotice);
        Assert.AreEqual("Ciphertext must be even.", vm.NoticeMessage);
    }

    [TestMethod]
    public void Copy_PutsOutputOnTheClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.Keyword = "playfair example";
        vm.InputText = "hi";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual(vm.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public void SwapDirection_FlipsDirectionAndFeedsOutputBackAsInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "playfair example";
        vm.InputText = "hide the gold in the tree stump";
        var cipher = vm.OutputText;

        vm.SwapDirectionCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual(cipher, vm.InputText);
        Assert.AreEqual("HIDETHEGOLDINTHETREXESTUMP", vm.OutputText);
    }

    [TestMethod]
    public void SkipFit_RemovesTheSkippedLetterFromTheSquare()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "keyword";
        vm.FitIndex = 1; // skip

        var letters = string.Concat(vm.SquareCells.Select(cell => cell.Letter));
        Assert.IsFalse(letters.Contains('Q'));
        Assert.IsTrue(letters.Contains('J'));
    }

    [TestMethod]
    public void Clear_EmptiesTheInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "hello";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
    }
}
