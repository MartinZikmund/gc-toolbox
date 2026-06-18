using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class AntipodesViewModelTests
{
    private static AntipodesViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        StubCatalogService catalog = new("Antipodes");
        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);
        IStringLocalizer localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["Antipodes_InvalidInput"] = "Could not parse the coordinate.",
            ["Antipodes_DistanceFormat"] = "About {0} km to the antipode.",
        });

        return new AntipodesViewModel(catalog, recents, favorites, localizer, clipboard, share);
    }

    [TestMethod]
    public void InputText_KnownVector_ProducesAntipodeInAllFormats()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "N50 25.123 E005 45.123";

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.AreEqual("S 50° 25.123' W 174° 14.877'", vm.OutputDdm);
        StringAssert.StartsWith(vm.OutputDd, "S 50.");
        StringAssert.Contains(vm.OutputDd, "W 174.");
        StringAssert.StartsWith(vm.OutputDms, "S 50° 25'");
    }

    [TestMethod]
    public void InputText_Empty_ClearsEverythingWithoutError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "N50 25.123 E005 45.123";

        vm.InputText = string.Empty;

        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(string.Empty, vm.OutputDdm);
        Assert.AreEqual(string.Empty, vm.ErrorMessage);
    }

    [TestMethod]
    public void InputText_Malformed_SetsErrorAndNoResult()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "not a coordinate";

        Assert.IsFalse(vm.HasResult);
        Assert.IsTrue(vm.HasError);
        Assert.AreEqual("Could not parse the coordinate.", vm.ErrorMessage);
    }

    [TestMethod]
    public void InputText_OutOfRangeLatitude_SetsError()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "N95 00.000 E005 00.000";

        Assert.IsFalse(vm.HasResult);
        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public void InputText_AcceptsDecimalDegrees()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "50.418717 5.752050";

        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual("S 50° 25.123' W 174° 14.877'", vm.OutputDdm);
    }

    [TestMethod]
    public void DistanceText_KnownVector_IsAboutTwentyThousandKm()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "N50 25.123 E005 45.123";

        Assert.IsFalse(string.IsNullOrEmpty(vm.DistanceText));
        StringAssert.Contains(vm.DistanceText, "20");
    }

    [TestMethod]
    public void CopyDdm_CopiesDdmOutput()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "N50 25.123 E005 45.123";

        vm.CopyDdmCommand.Execute(null);

        Assert.AreEqual(vm.OutputDdm, clipboard.LastText);
    }

    [TestMethod]
    public void CopyDd_CopiesDdOutput()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "N50 25.123 E005 45.123";

        vm.CopyDdCommand.Execute(null);

        Assert.AreEqual(vm.OutputDd, clipboard.LastText);
    }

    [TestMethod]
    public void CopyDms_CopiesDmsOutput()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "N50 25.123 E005 45.123";

        vm.CopyDmsCommand.Execute(null);

        Assert.AreEqual(vm.OutputDms, clipboard.LastText);
    }

    [TestMethod]
    public void Copy_WhenNoResult_CannotExecute()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.IsFalse(vm.CopyDdmCommand.CanExecute(null));
    }

    [TestMethod]
    public void Share_SharesDdmResult()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.InputText = "N50 25.123 E005 45.123";

        vm.ShareCommand.Execute(null);

        Assert.IsNotNull(share.LastText);
        StringAssert.Contains(share.LastText!, "S 50° 25.123'");
    }

    [TestMethod]
    public void Clear_ResetsInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "N50 25.123 E005 45.123";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void InputText_RoundTrip_AntipodeOfAntipodeReturnsSource()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "N50 25.123 E005 45.123";

        // Feeding the antipode back in must reproduce the original coordinate (own inverse).
        vm.InputText = vm.OutputDdm;

        Assert.AreEqual("N 50° 25.123' E 005° 45.123'", vm.OutputDdm);
    }
}
