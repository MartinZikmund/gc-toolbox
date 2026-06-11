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

    private FibonacciViewModel CreateViewModel() => new(
        new StubCatalogService("Fibonacci"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
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
