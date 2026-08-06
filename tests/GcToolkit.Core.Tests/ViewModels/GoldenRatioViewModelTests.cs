using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class GoldenRatioViewModelTests
{
    private const string First50 = "61803398874989484820458683436563811772030917980576";

    private static GoldenRatioViewModel CreateViewModel(GoldenRatioFakeClipboard? clipboard = null)
        => new(
            new StubCatalogService("GoldenRatio"),
            new GoldenRatioFakeRecents(),
            new GoldenRatioFakeFavorites(),
            new FakeStringLocalizer(),
            clipboard ?? new GoldenRatioFakeClipboard(),
            new GoldenRatioFakeShare());

    [TestMethod]
    public async Task ComputeAsync_FirstDecimalsGrouped_MatchesParityFormat()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;
        vm.CountText = "25";
        vm.GroupDigits = true;
        vm.ShowLineNumbers = false;

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.AreEqual("φ = 1.6180339887 4989484820 45868 (25)", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public async Task ComputeAsync_CountOutOfRange_ReportsError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;
        vm.CountText = "1000001";

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public async Task ComputeAsync_EmptyInput_ClearsOutputWithoutError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;
        vm.CountText = "";

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    [TestMethod]
    public async Task ComputeAsync_DigitAtPosition_ShowsDigitWithContext()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1;
        vm.PositionText = "1";

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasOutput);
        StringAssert.Contains(vm.OutputText, "[6]");
    }

    [TestMethod]
    public async Task ComputeAsync_Range_ReturnsInclusiveSlice()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;
        vm.RangeFromText = "11";
        vm.RangeToText = "20";
        vm.GroupDigits = false;
        vm.ShowLineNumbers = false;

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.AreEqual("4989484820", vm.OutputText);
    }

    [TestMethod]
    public async Task ComputeAsync_RangeInverted_ReportsError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;
        vm.RangeFromText = "20";
        vm.RangeToText = "11";

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public async Task ComputeAsync_Search_ListsOccurrencesWithSummary()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 3;
        vm.SearchText = "61803398874989";

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.ShowOccurrences);
        Assert.IsTrue(vm.Occurrences.Count >= 1);
        Assert.AreEqual("1", vm.Occurrences[0].Position);
    }

    [TestMethod]
    public async Task ComputeAsync_SearchNonDigits_ReportsError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 3;
        vm.SearchText = "12x4";

        await vm.ComputeCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.ShowOccurrences);
    }

    [TestMethod]
    public async Task ComputeAsync_StaleVersion_LastWriterWins()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;
        vm.CountText = "10";
        var first = vm.ComputeCommand.ExecuteAsync(null);
        vm.CountText = "25";

        await vm.ComputeCommand.ExecuteAsync(null);
        await first;

        Assert.AreEqual("φ = 1.6180339887 4989484820 45868 (25)", vm.OutputText);
    }

    [TestMethod]
    public async Task CopyOutput_WithResult_PutsOutputOnClipboard()
    {
        GoldenRatioFakeClipboard clipboard = new();
        var vm = CreateViewModel(clipboard);
        vm.ModeIndex = 0;
        vm.CountText = "50";
        vm.GroupDigits = false;
        await vm.ComputeCommand.ExecuteAsync(null);

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual($"φ = 1.{First50} (50)", clipboard.LastText);
    }

    private sealed class GoldenRatioFakeRecents : IRecentsService
    {
        public event EventHandler? RecentsChanged;

        public Task RecordOpenedAsync(string toolId)
        {
            RecentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class GoldenRatioFakeFavorites : IFavoriteToolsService
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

    private sealed class GoldenRatioFakeClipboard : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class GoldenRatioFakeShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }
}
