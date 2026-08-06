using GcToolkit.Core.Alphabets;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class SemaphoreViewModelTests
{
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeShareService _share = new();

    private SemaphoreViewModel CreateViewModel() => new(
        new StubCatalogService("Semaphore"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
        _clipboard,
        _share);

    private static SemaphoreFigureItem PaletteLetter(SemaphoreViewModel vm, char letter)
        => vm.Palette.First(i => i.Figure.Kind == SemaphoreFigureKind.Letter && i.Figure.Letter == letter);

    private static SemaphoreFigureItem PaletteSign(SemaphoreViewModel vm, SemaphoreFigureKind kind)
        => vm.Palette.First(i => i.Figure.Kind == kind);

    [TestMethod]
    public void InputText_CanonicalGc7aVector_RendersSevenFigures()
    {
        var vm = CreateViewModel();

        vm.InputText = "gc7 a";

        Assert.AreEqual(7, vm.Figures.Count);
        Assert.AreEqual("G", vm.Figures[0].Caption);
        Assert.AreEqual("C", vm.Figures[1].Caption);
        Assert.AreEqual(SemaphoreFigureKind.NumbersSign, vm.Figures[2].Figure.Kind);
        Assert.AreEqual("7", vm.Figures[3].Caption);
        Assert.AreEqual(SemaphoreFigureKind.Space, vm.Figures[4].Figure.Kind);
        Assert.AreEqual(SemaphoreFigureKind.LettersSign, vm.Figures[5].Figure.Kind);
        Assert.AreEqual("A", vm.Figures[6].Caption);
    }

    [TestMethod]
    public void InputText_LongText_StillRendersOneFigurePerCharacter()
    {
        var vm = CreateViewModel();

        vm.InputText = new string('a', 500);

        Assert.AreEqual(500, vm.Figures.Count);
    }

    [TestMethod]
    public void FigureItem_ToString_LeadsWithTheCaption()
    {
        var vm = CreateViewModel();

        var text = PaletteLetter(vm, 'A').ToString();

        Assert.IsTrue(text!.StartsWith("A / 1", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void Palette_HasAllLettersAndThreeSigns_WithDigitCaptions()
    {
        var vm = CreateViewModel();

        Assert.AreEqual(29, vm.Palette.Count);
        Assert.AreEqual("A / 1", PaletteLetter(vm, 'A').Caption);
        Assert.AreEqual("K / 0", PaletteLetter(vm, 'K').Caption);
        Assert.AreEqual("J", PaletteLetter(vm, 'J').Caption);
    }

    [TestMethod]
    public void SpecialSignals_AreCancelAndError_AndNotClickable()
    {
        var vm = CreateViewModel();

        Assert.AreEqual(2, vm.SpecialSignals.Count);
        Assert.AreEqual(SemaphoreFigureKind.Cancel, vm.SpecialSignals[0].Figure.Kind);
        Assert.AreEqual(SemaphoreFigureKind.Error, vm.SpecialSignals[1].Figure.Kind);
        Assert.IsTrue(vm.SpecialSignals.All(i => i.TapCommand is null), "Reference signals must not be tappable.");
    }

    [TestMethod]
    public void TapFigure_LetterInLettersMode_AppendsLowercaseLetter()
    {
        var vm = CreateViewModel();

        PaletteLetter(vm, 'A').TapCommand!.Execute(null);

        Assert.AreEqual("a", vm.InputText);
    }

    [TestMethod]
    public void TapFigure_NumbersSignThenLetter_AppendsDigit()
    {
        var vm = CreateViewModel();

        PaletteSign(vm, SemaphoreFigureKind.NumbersSign).TapCommand!.Execute(null);
        PaletteLetter(vm, 'A').TapCommand!.Execute(null);

        Assert.AreEqual("1", vm.InputText);
    }

    [TestMethod]
    public void TapFigure_LetterAfterDigitsInText_AppendsDigit()
    {
        var vm = CreateViewModel();
        vm.InputText = "a1";

        PaletteLetter(vm, 'B').TapCommand!.Execute(null);

        Assert.AreEqual("a12", vm.InputText);
    }

    [TestMethod]
    public void TapFigure_LettersSignLeavesNumberMode()
    {
        var vm = CreateViewModel();
        vm.InputText = "a1";

        PaletteSign(vm, SemaphoreFigureKind.LettersSign).TapCommand!.Execute(null);
        PaletteLetter(vm, 'B').TapCommand!.Execute(null);

        Assert.AreEqual("a1b", vm.InputText);
    }

    [TestMethod]
    public void TapFigure_SpaceKeepsNumberMode()
    {
        var vm = CreateViewModel();
        vm.InputText = "1";

        PaletteSign(vm, SemaphoreFigureKind.Space).TapCommand!.Execute(null);
        PaletteLetter(vm, 'A').TapCommand!.Execute(null);

        Assert.AreEqual("1 1", vm.InputText);
    }

    [TestMethod]
    public void TapFigure_LetterWithoutDigitInNumberMode_AppendsLetter()
    {
        var vm = CreateViewModel();

        PaletteSign(vm, SemaphoreFigureKind.NumbersSign).TapCommand!.Execute(null);
        PaletteLetter(vm, 'M').TapCommand!.Execute(null);

        Assert.AreEqual("m", vm.InputText);
    }

    [TestMethod]
    public void TapFigure_TypingAfterNumbersSign_ResetsPendingMode()
    {
        var vm = CreateViewModel();

        PaletteSign(vm, SemaphoreFigureKind.NumbersSign).TapCommand!.Execute(null);
        vm.InputText = "a"; // manual edit discards the pending Numbers tap
        PaletteLetter(vm, 'A').TapCommand!.Execute(null);

        Assert.AreEqual("aa", vm.InputText);
    }

    [TestMethod]
    public void Clear_ResetsTextAndFigures()
    {
        var vm = CreateViewModel();
        vm.InputText = "gc";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(0, vm.Figures.Count);
        Assert.IsFalse(vm.HasFigures);
    }

    [TestMethod]
    public void CopyText_CopiesInputText()
    {
        var vm = CreateViewModel();
        vm.InputText = "gc7";

        vm.CopyTextCommand.Execute(null);

        Assert.AreEqual("gc7", _clipboard.LastText);
    }

    [TestMethod]
    public void HasUnknown_UnmappableCharacter_IsTrue()
    {
        var vm = CreateViewModel();

        vm.InputText = "a?";

        Assert.IsTrue(vm.HasUnknown);
        Assert.AreEqual(1, vm.Figures.Count);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class FakeShareService : IShareService
    {
        public string? LastSharedText { get; private set; }

        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text)
        {
            LastSharedText = text;
            return Task.CompletedTask;
        }
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
