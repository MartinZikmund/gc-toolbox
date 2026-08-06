using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class FibonacciViewModelTests
{
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeShareService _share = new();

    private static readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
    {
        ["FibonacciIsMember"] = "{0} is F({1})",
        ["FibonacciIsNotMember"] = "{0} is not a Fibonacci number",
        ["FibonacciNearestBelow"] = "below: F({0}) = {1}",
        ["FibonacciNearestAbove"] = "above: F({0}) = {1}",
        ["FibonacciDigitsOne"] = "({0} digit)",
        ["FibonacciDigitsFew"] = "({0} digits)",
        ["FibonacciDigitsMany"] = "({0} digits)",
    };

    private FibonacciViewModel CreateViewModel() => new(
        new StubCatalogService("Fibonacci"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(_strings),
        _clipboard,
        _share);

    [TestMethod]
    public async Task RangeMode_SpanOfTenThousand_ProducesOneLinePerPosition()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;
        vm.ShowDigitCounts = false;

        vm.FromInput = "0";
        vm.ToInput = "9999";
        await vm.Computation;

        Assert.IsFalse(vm.HasError);
        Assert.IsTrue(vm.HasOutput);
        Assert.AreEqual(FibonacciViewModel.MaxRangeSpan, vm.ResultLines.Count);
        Assert.AreEqual(10_000, vm.ResultLines.Count);
    }

    [TestMethod]
    public async Task RangeMode_SpanBeyondMaximum_ReportsErrorAndNoLines()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;

        vm.FromInput = "0";
        vm.ToInput = (FibonacciViewModel.MaxRangeSpan).ToString(); // span = Max + 1
        await vm.Computation;

        Assert.IsTrue(vm.HasError);
        Assert.AreEqual(0, vm.ResultLines.Count);
    }

    [TestMethod]
    public async Task RangeMode_ProducesSelectableLines_WithPositionPrefix()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;
        vm.ShowDigitCounts = false;

        vm.FromInput = "0";
        vm.ToInput = "5";
        await vm.Computation;

        Assert.AreEqual(6, vm.ResultLines.Count);
        Assert.AreEqual("F(0) = 0", vm.ResultLines[0]);
        Assert.AreEqual("F(5) = 5", vm.ResultLines[5]);
    }

    [TestMethod]
    public async Task CopyOutput_JoinsResultLines()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;
        vm.ShowDigitCounts = false;

        vm.FromInput = "0";
        vm.ToInput = "2";
        await vm.Computation;

        vm.CopyOutputCommand.Execute(null);

        StringAssert.Contains(_clipboard.LastText, "F(0) = 0");
        StringAssert.Contains(_clipboard.LastText, "F(2) = 1");
    }

    [TestMethod]
    public async Task CheckMode_FibonacciValue_ReportsMembershipAndIndex()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;

        vm.ValueInput = "6765";
        await vm.Computation;

        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(1, vm.ResultLines.Count);
        Assert.AreEqual("6765 is F(20)", vm.ResultLines[0]);
    }

    [TestMethod]
    public async Task CheckMode_NonFibonacciValue_ReportsNearestNeighbours()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;

        vm.ValueInput = "100";
        await vm.Computation;

        Assert.IsFalse(vm.HasError);
        CollectionAssert.AreEqual(
            new[] { "100 is not a Fibonacci number", "below: F(11) = 89", "above: F(12) = 144" },
            vm.ResultLines.ToArray());
    }

    [TestMethod]
    public async Task CheckMode_NegativeValue_ReportsError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 0;

        vm.ValueInput = "-5";
        await vm.Computation;

        Assert.IsTrue(vm.HasError);
        Assert.AreEqual(0, vm.ResultLines.Count);
    }

    [TestMethod]
    public async Task AtIndexMode_WithDigitCounts_AnnotatesEachLine()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1;
        vm.ShowDigitCounts = true;

        vm.IndexInput = "12";
        await vm.Computation;

        Assert.AreEqual(1, vm.ResultLines.Count);
        Assert.AreEqual("F(12) = 144 (3 digits)", vm.ResultLines[0]);
    }

    [TestMethod]
    public async Task AtIndexMode_IndexBeyondMaximum_ReportsError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1;

        vm.IndexInput = "100001";
        await vm.Computation;

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public async Task ByDigitsMode_TwoDigits_ListsEveryTwoDigitMember()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 4;
        vm.ShowPositions = false;
        vm.ShowDigitCounts = false;

        vm.DigitsInput = "2";
        await vm.Computation;

        CollectionAssert.AreEqual(new[] { "13", "21", "34", "55", "89" }, vm.ResultLines.ToArray());
    }

    [TestMethod]
    public async Task Clear_ResetsInputsAndOutput()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2;
        vm.FromInput = "0";
        vm.ToInput = "5";
        await vm.Computation;
        Assert.IsTrue(vm.HasOutput);

        vm.ClearCommand.Execute(null);
        await vm.Computation;

        Assert.AreEqual(string.Empty, vm.FromInput);
        Assert.AreEqual(string.Empty, vm.ToInput);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

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

        public Task RecordOpenedAsync(string toolId)
        {
            RecentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakeFavoriteToolsService : IFavoriteToolsService
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
