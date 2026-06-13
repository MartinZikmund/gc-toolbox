using System.Linq;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class ColourConversionViewModelTests
{
    private static ColourConversionViewModel CreateViewModel(out CapturingClipboard clipboard)
    {
        clipboard = new CapturingClipboard();
        return new ColourConversionViewModel(
            new StubCatalogService("ColourConversion"),
            new NoopRecents(),
            new NoopFavorites(),
            new FakeStringLocalizer(),
            clipboard,
            new NoopShare());
    }

    private static ColourField Field(ColourConversionViewModel vm, string title, int index)
        => vm.Models.First(m => m.TitleKey == title).Fields[index];

    [TestMethod]
    public void Constructor_DefaultsToOrange_PopulatesEveryField()
    {
        var vm = CreateViewModel(out _);

        Assert.AreEqual("#FF8000", vm.HexField.Value);
        Assert.AreEqual("255", Field(vm, "ColourConversionRgb", 0).Value);
        Assert.AreEqual("128", Field(vm, "ColourConversionRgb", 1).Value);
        Assert.AreEqual("0", Field(vm, "ColourConversionRgb", 2).Value);
        Assert.AreEqual("#FFFF8000", vm.SwatchArgb);
    }

    [TestMethod]
    public void EditHex_RecomputesRgbAndSwatch()
    {
        var vm = CreateViewModel(out _);

        vm.HexField.Value = "#00FF00";

        Assert.AreEqual("0", Field(vm, "ColourConversionRgb", 0).Value);
        Assert.AreEqual("255", Field(vm, "ColourConversionRgb", 1).Value);
        Assert.AreEqual("0", Field(vm, "ColourConversionRgb", 2).Value);
        Assert.AreEqual("#FF00FF00", vm.SwatchArgb);
    }

    [TestMethod]
    public void EditRgb_RecomputesHexAndOtherModels()
    {
        var vm = CreateViewModel(out _);

        Field(vm, "ColourConversionRgb", 0).Value = "255";
        Field(vm, "ColourConversionRgb", 1).Value = "0";
        Field(vm, "ColourConversionRgb", 2).Value = "0";

        // Pure red: hex updates, and HSL hue = 0, sat = 100, light = 50.
        Assert.AreEqual("#FF0000", vm.HexField.Value);
        Assert.AreEqual("0", Field(vm, "ColourConversionHsl", 0).Value);
        Assert.AreEqual("100", Field(vm, "ColourConversionHsl", 1).Value);
        Assert.AreEqual("50", Field(vm, "ColourConversionHsl", 2).Value);
    }

    [TestMethod]
    public void EditHsl_RecomputesRgb_AnyToAny()
    {
        var vm = CreateViewModel(out _);

        Field(vm, "ColourConversionHsl", 0).Value = "240"; // hue
        Field(vm, "ColourConversionHsl", 1).Value = "100"; // sat
        Field(vm, "ColourConversionHsl", 2).Value = "50";  // lightness -> pure blue

        Assert.AreEqual("#0000FF", vm.HexField.Value);
    }

    [TestMethod]
    public void EditInvalidHex_LeavesPreviousColourUnchanged()
    {
        var vm = CreateViewModel(out _);

        vm.HexField.Value = "not-a-colour";

        // RGB stays at the orange default; no exception thrown.
        Assert.AreEqual("255", Field(vm, "ColourConversionRgb", 0).Value);
    }

    [TestMethod]
    public void CopyAll_WritesEveryModelLine()
    {
        var vm = CreateViewModel(out var clipboard);

        vm.CopyAllCommand.Execute(null);

        var text = clipboard.LastText;
        Assert.IsTrue(text!.Contains("Hex:"), "Hex");
        Assert.IsTrue(text.Contains("RGB:"), "RGB");
        Assert.IsTrue(text.Contains("YCbCr:"), "YCbCr");
    }

    [TestMethod]
    public void Models_AreTheTenReferenceModelsInOrder()
    {
        var vm = CreateViewModel(out _);

        CollectionAssert.AreEqual(
            new[]
            {
                "ColourConversionRgb", "ColourConversionCmy", "ColourConversionCmyk",
                "ColourConversionHsl", "ColourConversionHsv", "ColourConversionHsi",
                "ColourConversionYiq", "ColourConversionYuv", "ColourConversionYCbCr",
            },
            vm.Models.Select(m => m.TitleKey).ToArray());
    }

    private sealed class CapturingClipboard : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class NoopShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }

    private sealed class NoopRecents : IRecentsService
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

    private sealed class NoopFavorites : IFavoriteToolsService
    {
        public event EventHandler? FavoriteToolsChanged;

        public bool IsFavorite(string toolId) => false;

        public IReadOnlyList<string> GetFavoriteToolIds() => [];

        public Task ToggleAsync(string toolId)
        {
            FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
