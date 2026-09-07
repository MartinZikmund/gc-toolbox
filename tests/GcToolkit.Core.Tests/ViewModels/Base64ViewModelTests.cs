using System.ComponentModel;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class Base64ViewModelTests
{
    // Mirrors Base64ViewModel's documented DirectionIndex: 0 = auto-detect, 1 = encode, 2 = decode.
    private const int Auto = 0;
    private const int Encode = 1;
    private const int Decode = 2;

    private Base64ViewModel CreateViewModel() => new(
        new StubCatalogService("Base64"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
        new FakeClipboardService(),
        new FakeShareService());

    [TestMethod]
    public void Swap_AfterEncoding_FeedsTheResultBackAndDecodesIt()
    {
        var vm = CreateViewModel();
        vm.DirectionIndex = Encode;
        vm.InputText = "cache";
        Assert.AreEqual("Y2FjaGU=", vm.OutputText);

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(Decode, vm.DirectionIndex);
        Assert.AreEqual("Y2FjaGU=", vm.InputText);
        Assert.AreEqual("cache", vm.OutputText);
        Assert.IsFalse(vm.IsEncoding);
    }

    [TestMethod]
    public void Swap_AfterDecoding_FlipsBackToEncode()
    {
        var vm = CreateViewModel();
        vm.DirectionIndex = Decode;
        vm.InputText = "Y2FjaGU=";
        Assert.AreEqual("cache", vm.OutputText);

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(Encode, vm.DirectionIndex);
        Assert.AreEqual("cache", vm.InputText);
        Assert.AreEqual("Y2FjaGU=", vm.OutputText);
        Assert.IsTrue(vm.IsEncoding);
    }

    [TestMethod]
    public void Swap_Twice_ReturnsToTheOriginalText()
    {
        var vm = CreateViewModel();
        vm.DirectionIndex = Encode;
        vm.InputText = "cache";

        vm.SwapCommand.Execute(null);
        vm.SwapCommand.Execute(null);

        Assert.AreEqual(Encode, vm.DirectionIndex);
        Assert.AreEqual("cache", vm.InputText);
        Assert.AreEqual("Y2FjaGU=", vm.OutputText);
    }

    [TestMethod]
    public void Swap_WritesDirectionAndInput_ButRecomputesOnlyOnce()
    {
        var vm = CreateViewModel();
        vm.DirectionIndex = Encode;
        vm.InputText = "cache";

        var outputChanges = 0;
        vm.PropertyChanged += Count;
        vm.SwapCommand.Execute(null);
        vm.PropertyChanged -= Count;

        // Both writes feed the same recompute; without the guard the half-swapped
        // pass (decode of "cache") would publish an extra OutputText.
        Assert.AreEqual(1, outputChanges);

        void Count(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Base64ViewModel.OutputText))
            {
                outputChanges++;
            }
        }
    }

    [TestMethod]
    public void Swap_WithoutOutput_CannotExecute()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.SwapCommand.CanExecute(null));

        vm.DirectionIndex = Encode;
        vm.InputText = "cache";

        Assert.IsTrue(vm.SwapCommand.CanExecute(null));
    }

    [TestMethod]
    public void Swap_UnderAutoDetect_PinsTheDirectionItSwapsTo()
    {
        var vm = CreateViewModel();
        vm.DirectionIndex = Auto;
        vm.InputText = "hello world";
        Assert.IsTrue(vm.IsEncoding, "A sentence with a space is not Base64.");

        vm.SwapCommand.Execute(null);

        // Auto-detect is left behind: the swap states the direction outright.
        Assert.AreEqual(Decode, vm.DirectionIndex);
        Assert.IsFalse(vm.ShowDetectedDirection);
        Assert.AreEqual("hello world", vm.OutputText);
    }
}
