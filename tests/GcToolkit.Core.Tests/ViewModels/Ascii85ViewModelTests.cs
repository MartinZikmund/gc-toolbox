using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class Ascii85ViewModelTests
{
    private static Ascii85ViewModel CreateViewModel(out RecordingClipboard clipboard)
    {
        var descriptor = new ToolDescriptor(
            "Ascii85", "Ascii85_Name", "Ciphers", new string[0], "Ascii85", typeof(object), false, "Ascii85_Tooltip");

        CatalogService catalog = new(
            [new StubCategoryContributor(new Category("Ciphers", "Ciphers_Name", 0, "Ciphers"))],
            [new StubToolContributor(descriptor)],
            new ToolMatcher(),
            new FakeStringLocalizer());

        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);

        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            ["Ascii85_Name"] = "ASCII-85",
            ["Ascii85UnitBytes"] = "bytes",
            ["Ascii85UnitChars"] = "chars",
        });

        clipboard = new RecordingClipboard();
        Ascii85ViewModel vm = new(catalog, recents, favorites, localizer, clipboard, new NoopShare());
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Encrypt_AdobeText_ProducesAscii85AndHasOutput()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 0;
        vm.VariantIndex = 0;

        vm.InputText = "Man ";

        Assert.AreEqual("9jqo^", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Decrypt_ValidAdobe_RecoversTextAndShowsHex()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1;
        vm.VariantIndex = 0;

        vm.InputText = "9jqo^";

        Assert.AreEqual("Man ", vm.OutputText);
        Assert.IsTrue(vm.ShowHexView);
        Assert.AreEqual("4D 61 6E 20", vm.HexView);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Decrypt_IllegalCharacter_SetsError()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1;
        vm.VariantIndex = 0;

        vm.InputText = "9jqv"; // 'v' is above Adobe's ceiling

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void SwitchingDirection_CarriesOutputIntoInput_RoundTrips()
    {
        var vm = CreateViewModel(out _);
        vm.VariantIndex = 0;
        vm.InputText = "Cat";
        var encoded = vm.OutputText;

        vm.DirectionIndex = 1; // Decrypt now decodes the carried-over ciphertext

        Assert.AreEqual(encoded, vm.InputText);
        Assert.AreEqual("Cat", vm.OutputText);
    }

    [TestMethod]
    public void AutoDetect_AdobeDelimited_SelectsAdobe()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1;
        vm.VariantIndex = 2; // start on Z85
        vm.InputText = "<~9jqo^~>";

        vm.AutoDetectCommand.Execute(null);

        Assert.AreEqual(0, vm.VariantIndex);
    }

    [TestMethod]
    public void ShowAllVariants_PopulatesOneRowPerVariant()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1;
        vm.InputText = "9jqo^";

        vm.ShowAllVariants = true;

        Assert.AreEqual(4, vm.AllVariantResults.Count);
    }

    [TestMethod]
    public void CopyOutput_WritesOutputToClipboard()
    {
        var vm = CreateViewModel(out var clipboard);
        vm.InputText = "Man ";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("9jqo^", clipboard.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = CreateViewModel(out _);
        vm.InputText = "Man ";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class NoopShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }
}
