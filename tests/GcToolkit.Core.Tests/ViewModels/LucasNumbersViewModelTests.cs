using System.Globalization;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>
/// Exercises <see cref="LucasNumbersViewModel"/> with hand-written fakes. The heavy lifting lives in
/// <c>LucasNumberSequence</c>; these tests cover the thin VM glue — mode switching, parsing, validation
/// messages, the show-position / show-digit-count toggles, and copy/share output.
/// </summary>
[TestClass]
public sealed class LucasNumbersViewModelTests
{
    [TestInitialize]
    public void UseInvariantCulture()
    {
        // Fix the culture so thousands-separator formatting is deterministic across machines.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestMethod]
    public void Position_ValidIndex_ShowsSingleResult()
    {
        var sut = CreateSut();
        sut.ModeIndex = 0;
        sut.PositionInput = "10";

        Assert.IsTrue(sut.ShowSingleResult);
        Assert.AreEqual("123", sut.SingleResultValue);
        Assert.IsTrue(sut.HasResults);
        Assert.IsFalse(sut.HasError);
    }

    [TestMethod]
    public void Position_Zero_ReturnsTwo()
    {
        var sut = CreateSut();
        sut.ModeIndex = 0;
        sut.PositionInput = "0";

        Assert.AreEqual("2", sut.SingleResultValue);
    }

    [TestMethod]
    public void Position_NegativeIndex_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 0;
        sut.PositionInput = "-3";

        Assert.IsTrue(sut.HasError);
        Assert.IsFalse(sut.HasResults);
        Assert.AreEqual("LucasErrorInvalidIndex", sut.ErrorMessage);
    }

    [TestMethod]
    public void Position_NonNumeric_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 0;
        sut.PositionInput = "abc";

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorInvalidIndex", sut.ErrorMessage);
    }

    [TestMethod]
    public void Position_Empty_ShowsNeitherResultNorError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 0;
        sut.PositionInput = "";

        Assert.IsFalse(sut.HasResults);
        Assert.IsFalse(sut.HasError);
    }

    [TestMethod]
    public void Range_InclusiveBounds_PopulatesResults()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1;
        sut.RangeStartInput = "2";
        sut.RangeEndInput = "5";

        Assert.AreEqual(4, sut.Results.Count);
        Assert.AreEqual(2, sut.Results[0].Index);
        Assert.AreEqual("3", sut.Results[0].Value);
        Assert.AreEqual(5, sut.Results[3].Index);
        Assert.AreEqual("11", sut.Results[3].Value);
        Assert.IsFalse(sut.ShowSingleResult);
        Assert.IsTrue(sut.HasResults);
    }

    [TestMethod]
    public void Range_StartAfterEnd_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1;
        sut.RangeStartInput = "9";
        sut.RangeEndInput = "2";

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorStartAfterEnd", sut.ErrorMessage);
        Assert.AreEqual(0, sut.Results.Count);
    }

    [TestMethod]
    public void Range_TooLarge_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1;
        sut.RangeStartInput = "0";
        sut.RangeEndInput = (LucasNumbersViewModel.MaxRangeSpan + 5).ToString(CultureInfo.InvariantCulture);

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorRangeTooLarge", sut.ErrorMessage);
    }

    [TestMethod]
    public void Range_DigitCountColumn_ReflectsValueLength()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1;
        sut.RangeStartInput = "9";
        sut.RangeEndInput = "10";

        Assert.AreEqual(2, sut.Results[0].DigitCount); // L9 = 76
        Assert.AreEqual(3, sut.Results[1].DigitCount); // L10 = 123
    }

    [TestMethod]
    public void ByValue_LucasNumber_ResolvesIndex()
    {
        var sut = CreateSut();
        sut.ModeIndex = 2;
        sut.ValueInput = "123";

        Assert.IsTrue(sut.ShowSingleResult);
        Assert.AreEqual("10", sut.SingleResultValue);
        Assert.IsTrue(sut.HasResults);
    }

    [TestMethod]
    public void ByValue_NotALucasNumber_ShowsClearError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 2;
        sut.ValueInput = "100";

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorNotLucas", sut.ErrorMessage);
        Assert.IsFalse(sut.HasResults);
    }

    [TestMethod]
    public void ByValue_NonNumeric_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 2;
        sut.ValueInput = "xyz";

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorInvalidValue", sut.ErrorMessage);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-7")]
    public void ByValue_NonPositive_ShowsInvalidValueError(string input)
    {
        // Non-positive input is invalid, not merely "not a Lucas number" — guard before the lookup.
        var sut = CreateSut();
        sut.ModeIndex = 2;
        sut.ValueInput = input;

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorInvalidValue", sut.ErrorMessage);
        Assert.IsFalse(sut.HasResults);
    }

    [TestMethod]
    public void ByDigitCount_WhitespacePaddedInput_Parses()
    {
        var sut = CreateSut();
        sut.ModeIndex = 3;
        sut.DigitCountInput = "  1  ";

        Assert.IsFalse(sut.HasError);
        Assert.AreEqual(5, sut.Results.Count);
    }

    [TestMethod]
    public void ByDigitCount_One_ReturnsFiveValues()
    {
        var sut = CreateSut();
        sut.ModeIndex = 3;
        sut.DigitCountInput = "1";

        Assert.AreEqual(5, sut.Results.Count);
        Assert.AreEqual(0, sut.Results[0].Index);
        Assert.AreEqual("2", sut.Results[0].Value);
    }

    [TestMethod]
    public void ByDigitCount_Three_ReturnsExpectedIndices()
    {
        var sut = CreateSut();
        sut.ModeIndex = 3;
        sut.DigitCountInput = "3";

        Assert.AreEqual(5, sut.Results.Count);
        Assert.AreEqual(10, sut.Results[0].Index);
        Assert.AreEqual(14, sut.Results[4].Index);
    }

    [TestMethod]
    public void ByDigitCount_Invalid_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 3;
        sut.DigitCountInput = "0";

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual("LucasErrorInvalidDigitCount", sut.ErrorMessage);
    }

    [TestMethod]
    public void Copy_RangeWithBothColumns_WritesFormattedOutput()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateSut(clipboard: clipboard);
        sut.ShowPosition = true;
        sut.ShowDigitCount = true;
        sut.ModeIndex = 1;
        sut.RangeStartInput = "0";
        sut.RangeEndInput = "2";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual("L(0) = 2 (1)\nL(1) = 1 (1)\nL(2) = 3 (1)", clipboard.LastText);
    }

    [TestMethod]
    public void Copy_PositionMode_WritesCaptionAndValue()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateSut(clipboard: clipboard);
        sut.ModeIndex = 0;
        sut.PositionInput = "10";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual("L(10) = 123", clipboard.LastText);
    }

    [TestMethod]
    public void CopyCommand_NoResults_CannotExecute()
    {
        var sut = CreateSut();
        sut.ModeIndex = 0;
        sut.PositionInput = "";

        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void SwitchingMode_ClearsPreviousError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 2;
        sut.ValueInput = "100"; // not a Lucas number
        Assert.IsTrue(sut.HasError);

        sut.ModeIndex = 0; // position mode with default "10"

        Assert.IsFalse(sut.HasError);
        Assert.IsTrue(sut.HasResults);
    }

    private static LucasNumbersViewModel CreateSut(FakeClipboardService? clipboard = null)
        => new(
            new StubCatalogService("LucasNumbers"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["LucasPositionCaption"] = "L({0})",
                ["LucasValueCaption"] = "Index of {0}",
            }),
            clipboard ?? new FakeClipboardService(),
            new FakeShareService());

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class FakeShareService : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }

    private sealed class FakeRecentsService : IRecentsService
    {
        public event EventHandler? RecentsChanged;

        public Task RecordOpenedAsync(string toolId) => Task.CompletedTask;

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync()
        {
            RecentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFavoriteToolsService : IFavoriteToolsService
    {
        public event EventHandler? FavoriteToolsChanged;

        public bool IsFavorite(string toolId) => false;

        public Task ToggleAsync(string toolId)
        {
            FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetFavoriteToolIds() => [];
    }
}
