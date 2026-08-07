using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Text;

/// <summary>
/// Covers the ASCII converter's UX contract: live conversion as the input changes, the any-to-any
/// From/To matrix, auto-detection, the "remove spaces" option, error states, and the swap round-trip.
/// </summary>
[TestClass]
public sealed class AsciiConverterViewModelTests
{
    // Indices into the To dropdown: 0 Text, 1 Binary, 2 Octal, 3 Decimal, 4 Hex, 5 Base32, 6 Base64, 7 HTML.
    private const int ToText = 0;
    private const int ToBinary = 1;
    private const int ToDecimal = 3;
    private const int ToHex = 4;
    private const int ToBase64 = 6;

    // The From dropdown is the same list prefixed by "auto", so every index is shifted by one.
    private const int FromAuto = 0;
    private const int FromText = 1;
    private const int FromBinary = 2;
    private const int FromDecimal = 4;
    private const int FromBase64 = 7;

    [TestMethod]
    public void Defaults_ConvertTextToDecimalLiveAsTheInputChanges()
    {
        var sut = CreateSut();

        sut.InputText = "Hi";

        Assert.AreEqual("72 105", sut.OutputText);
        Assert.IsTrue(sut.HasOutput);
        Assert.IsFalse(sut.HasError);
    }

    [TestMethod]
    public void ChangingTheTargetFormat_ReconvertsTheSameInput()
    {
        var sut = CreateSut();
        sut.InputText = "Hi";

        sut.ToFormatIndex = ToHex;

        Assert.AreEqual("48 69", sut.OutputText);
    }

    [TestMethod]
    public void BaseToBase_ConvertsBinaryCodeGroupsToHexCodeGroups()
    {
        var sut = CreateSut();
        sut.FromFormatIndex = FromBinary;
        sut.ToFormatIndex = ToHex;

        sut.InputText = "1001000 1101001";

        Assert.AreEqual("48 69", sut.OutputText);
    }

    [TestMethod]
    public void Base64_DecodesToText()
    {
        var sut = CreateSut();
        sut.FromFormatIndex = FromBase64;
        sut.ToFormatIndex = ToText;

        sut.InputText = "SGVsbG8=";

        Assert.AreEqual("Hello", sut.OutputText);
    }

    [TestMethod]
    public void AutoDetect_ReportsTheDetectedInputFormat()
    {
        var sut = CreateSut();
        sut.FromFormatIndex = FromAuto;
        sut.ToFormatIndex = ToText;

        sut.InputText = "72 105";

        Assert.AreEqual("Hi", sut.OutputText);
        Assert.IsTrue(sut.ShowDetectedFormat);
        Assert.AreNotEqual(string.Empty, sut.DetectedFormatName);
    }

    [TestMethod]
    public void CustomSeparator_IsUsedBetweenCodeGroups()
    {
        var sut = CreateSut();
        sut.InputText = "Hi";

        sut.Separator = ",";

        Assert.AreEqual("72,105", sut.OutputText);
    }

    [TestMethod]
    public void RemoveSpaces_StripsSeparatorSpacesFromTheResult()
    {
        var sut = CreateSut();
        sut.InputText = "Hi";

        sut.RemoveSpaces = true;

        Assert.AreEqual("72105", sut.OutputText);
    }

    [TestMethod]
    public void SeparatorIsHiddenForFormatsThatAreNotGrouped()
    {
        var sut = CreateSut();
        Assert.IsTrue(sut.ShowSeparator, "decimal groups per character, so the separator applies");

        sut.ToFormatIndex = ToBase64;

        Assert.IsFalse(sut.ShowSeparator);
    }

    [TestMethod]
    public void InvalidInputForTheChosenFormat_ShowsAnErrorAndNoOutput()
    {
        var sut = CreateSut();
        sut.FromFormatIndex = FromDecimal;

        sut.InputText = "not a number";

        Assert.IsTrue(sut.HasError);
        Assert.AreNotEqual(string.Empty, sut.ErrorMessage);
        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.IsFalse(sut.HasOutput);
        Assert.AreEqual(0, sut.Breakdown.Count);
    }

    [TestMethod]
    public void EmptyInput_ClearsOutputErrorAndBreakdown()
    {
        var sut = CreateSut();
        sut.InputText = "Hi";

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.InputText);
        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.IsFalse(sut.HasOutput);
        Assert.IsFalse(sut.HasError);
        Assert.IsFalse(sut.HasBreakdown);
    }

    [TestMethod]
    public void Breakdown_HasOneRowPerCodePointOfTheDecodedText()
    {
        var sut = CreateSut();

        sut.InputText = "Hi";

        Assert.IsTrue(sut.HasBreakdown);
        Assert.AreEqual(2, sut.Breakdown.Count);
        Assert.AreEqual("H", sut.Breakdown[0].Character);
        Assert.AreEqual("1101001", sut.Breakdown[1].Binary);
    }

    [TestMethod]
    public void Swap_ExchangesFormatsAndFeedsTheResultBackForAOneTapRoundTrip()
    {
        var sut = CreateSut();
        sut.FromFormatIndex = FromText;
        sut.ToFormatIndex = ToBinary;
        sut.InputText = "Hi";
        Assert.AreEqual("1001000 1101001", sut.OutputText);

        sut.SwapCommand.Execute(null);

        Assert.AreEqual(FromBinary, sut.FromFormatIndex);
        Assert.AreEqual(ToText, sut.ToFormatIndex);
        Assert.AreEqual("1001000 1101001", sut.InputText);
        Assert.AreEqual("Hi", sut.OutputText);
    }

    [TestMethod]
    public void Swap_UnderAutoDetect_UsesTheDetectedFormatAsTheNewTarget()
    {
        var sut = CreateSut();
        sut.FromFormatIndex = FromAuto;
        sut.ToFormatIndex = ToText;
        sut.InputText = "72 105";

        sut.SwapCommand.Execute(null);

        Assert.AreEqual(FromText, sut.FromFormatIndex);
        Assert.AreEqual(ToDecimal, sut.ToFormatIndex);
        Assert.AreEqual("72 105", sut.OutputText);
    }

    [TestMethod]
    public void CopyAndShare_AreDisabledUntilThereIsOutput()
    {
        var sut = CreateSut();
        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(sut.ShareOutputCommand.CanExecute(null));
        Assert.IsFalse(sut.SwapCommand.CanExecute(null));

        sut.InputText = "Hi";

        Assert.IsTrue(sut.CopyOutputCommand.CanExecute(null));
        Assert.IsTrue(sut.ShareOutputCommand.CanExecute(null));
        Assert.IsTrue(sut.SwapCommand.CanExecute(null));
    }

    [TestMethod]
    public void CopyOutput_PutsTheResultOnTheClipboard()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateSut(clipboard);
        sut.InputText = "Hi";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual("72 105", clipboard.LastText);
    }

    private static AsciiConverterViewModel CreateSut(FakeClipboardService? clipboard = null)
        => new(
            new StubCatalogService(),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard ?? new FakeClipboardService(),
            new FakeShareService());
}
