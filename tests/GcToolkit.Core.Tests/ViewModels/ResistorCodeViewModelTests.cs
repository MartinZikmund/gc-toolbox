using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>
/// Exercises <see cref="ResistorCodeViewModel"/> wiring (mode switching, live recompute, band rebuild,
/// copy) with hand-written fakes. The numeric tables are covered exhaustively by <c>ResistorCodeTests</c>.
/// </summary>
[TestClass]
public sealed class ResistorCodeViewModelTests
{
    private const string ToolId = "ResistorCode";

    [TestMethod]
    public void Decode_DefaultFourBand_ShowsResistanceAndTolerance()
    {
        var sut = CreateSut();

        // Defaults: yellow, violet, brown multiplier, gold tolerance -> 470 Ω, ±5%.
        Assert.IsTrue(sut.HasOutput);
        Assert.IsFalse(sut.HasError);
        StringAssert.Contains(sut.ResistanceText, "470");
        StringAssert.Contains(sut.ToleranceText, "5");
    }

    [TestMethod]
    public void Decode_ChangingABand_RecomputesLive()
    {
        var sut = CreateSut();

        // Change the multiplier band (index 2) from brown (x10) to red (x100): 47 x100 = 4.7 kΩ.
        var multiplier = sut.Bands[2];
        multiplier.SelectedOption = multiplier.Options.First(o => o.Color == ResistorColor.Red);

        StringAssert.Contains(sut.ResistanceText, "4.7");
        StringAssert.Contains(sut.ResistanceText, "k");
    }

    [TestMethod]
    public void Decode_SixBand_AddsTemperatureCoefficient()
    {
        var sut = CreateSut();
        sut.BandCountIndex = 2; // 6-band

        Assert.AreEqual(6, sut.Bands.Count);
        Assert.IsTrue(sut.HasTempCo);
        Assert.IsFalse(string.IsNullOrEmpty(sut.TempCoText));
    }

    [TestMethod]
    public void Encode_ValueAndTolerance_ProducesBands()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1; // encode
        sut.SelectedTolerance = sut.ToleranceOptions.First(o => o.Color == ResistorColor.Gold);
        sut.ResistanceInput = "4700";

        Assert.IsTrue(sut.HasOutput);
        Assert.AreEqual(4, sut.ResultBands.Count);
        CollectionAssert.AreEqual(
            new[] { ResistorColor.Yellow, ResistorColor.Violet, ResistorColor.Red, ResistorColor.Gold },
            sut.ResultBands.Select(b => b.Color).ToArray());
    }

    [TestMethod]
    public void Encode_SuffixInput_IsParsed()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1;
        sut.ResistanceInput = "4.7k";

        Assert.IsTrue(sut.HasOutput);
        StringAssert.Contains(sut.ResistanceText, "4.7");
        StringAssert.Contains(sut.ResistanceText, "k");
    }

    [TestMethod]
    public void Encode_GarbageInput_ShowsError()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1;
        sut.ResistanceInput = "not-a-number";

        Assert.IsTrue(sut.HasError);
        Assert.IsFalse(sut.HasOutput);
    }

    [TestMethod]
    public void CopyOutput_PutsResultOnClipboard()
    {
        var clipboard = new FakeClipboard();
        var sut = CreateSut(clipboard: clipboard);

        sut.CopyOutputCommand.Execute(null);

        Assert.IsFalse(string.IsNullOrEmpty(clipboard.LastText));
        StringAssert.Contains(clipboard.LastText!, "470");
    }

    [TestMethod]
    public void CopyOutput_CanExecute_TracksHasOutput()
    {
        var sut = CreateSut();
        sut.ModeIndex = 1; // encode with empty input -> no output
        sut.ResistanceInput = string.Empty;

        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));

        sut.ResistanceInput = "1000";
        Assert.IsTrue(sut.CopyOutputCommand.CanExecute(null));
    }

    private static ResistorCodeViewModel CreateSut(FakeClipboard? clipboard = null)
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{ToolId}_Name"] = "Resistor colour code",
            [$"{ToolId}_Tooltip"] = "Decode and encode resistor bands",
            ["ResistorBandDigit"] = "Digit {0}",
            ["ResistorBandMultiplier"] = "Multiplier",
            ["ResistorBandTolerance"] = "Tolerance",
            ["ResistorBandTempCo"] = "Temp. coefficient",
            ["ResistorToleranceValue"] = "± {0}%",
            ["ResistorTempCoValue"] = "{0} ppm/K",
        });

        return new ResistorCodeViewModel(
            new StubCatalogService(ToolId),
            new FakeRecents(),
            new FakeFavorites(),
            localizer,
            clipboard ?? new FakeClipboard(),
            new FakeShare());
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class FakeShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }

    private sealed class FakeRecents : IRecentsService
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

    private sealed class FakeFavorites : IFavoriteToolsService
    {
        private readonly HashSet<string> _favorites = [];

        public event EventHandler? FavoriteToolsChanged;

        public bool IsFavorite(string toolId) => _favorites.Contains(toolId);

        public Task ToggleAsync(string toolId)
        {
            if (!_favorites.Add(toolId))
            {
                _favorites.Remove(toolId);
            }

            FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetFavoriteToolIds() => [.. _favorites];
    }
}
