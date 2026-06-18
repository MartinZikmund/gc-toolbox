using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels.Tools;

[TestClass]
public class BeaufortCipherViewModelTests
{
    private static BeaufortCipherViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        var descriptor = new ToolDescriptor(
            "BeaufortCipher", "BeaufortCipher_Name", "ciphers", [], "BeaufortCipher",
            typeof(BeaufortCipherViewModel), TooltipKey: "BeaufortCipher_Tooltip");

        var localizer = new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BeaufortCipher_Name"] = "Beaufort Cipher",
            ["BeaufortCipher_Tooltip"] = "Reciprocal reversed Vigenère",
            ["BeaufortExplainerStandard"] = "Standard explainer",
            ["BeaufortExplainerVariant"] = "Variant explainer",
            ["BeaufortExplainerAutokey"] = "Autokey explainer",
            ["BeaufortEmptyKeyword"] = "Enter a keyword.",
            ["BeaufortInvalidKeyword"] = "Keyword has invalid letters.",
            ["BeaufortInvalidAlphabet"] = "Custom alphabet is invalid.",
        });

        var vm = new BeaufortCipherViewModel(
            new SingleToolCatalog(descriptor),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            localizer,
            clipboard,
            share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Standard_EnciphersLiveAsInputChanges()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "KEY";
        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasOutput);
        Assert.AreEqual(new BeaufortCipher().Encrypt("HELLO", "KEY"), vm.OutputText);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void Standard_IsReciprocal_NoDirectionNeeded()
    {
        var vm = CreateViewModel(out _, out _);
        Assert.IsTrue(vm.IsReciprocal);

        vm.Keyword = "CACHE";
        vm.InputText = "GEOCACHE";
        var encrypted = vm.OutputText;

        // Re-feeding the cipher text reproduces the plaintext (self-reciprocal).
        vm.InputText = encrypted;
        Assert.AreEqual("GEOCACHE", vm.OutputText);
    }

    [TestMethod]
    public void Variant_UsesDirectionToDecrypt()
    {
        var vm = CreateViewModel(out _, out _);
        vm.VariantIndex = 1; // Variant
        Assert.IsFalse(vm.IsReciprocal);

        vm.Keyword = "BEAUFORT";
        vm.InputText = "GEOCACHE";
        var encrypted = vm.OutputText;

        vm.DirectionIndex = 1; // Decrypt
        vm.InputText = encrypted;
        Assert.AreEqual("GEOCACHE", vm.OutputText);
    }

    [TestMethod]
    public void Autokey_RoundTripsWithDirection()
    {
        var vm = CreateViewModel(out _, out _);
        vm.VariantIndex = 2; // Autokey
        vm.Keyword = "TREASURE";
        vm.InputText = "MEETATTHECACHE";
        var encrypted = vm.OutputText;

        vm.DirectionIndex = 1; // Decrypt
        vm.InputText = encrypted;
        Assert.AreEqual("MEETATTHECACHE", vm.OutputText);
    }

    [TestMethod]
    public void EmptyKeyword_WithInput_ShowsWarningNoOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    [TestMethod]
    public void InvalidKeywordCharacter_ShowsWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "KE1";
        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void EmptyInput_ClearsResultWithoutWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "KEY";
        vm.InputText = "HELLO";
        Assert.IsTrue(vm.HasOutput);

        vm.InputText = string.Empty;
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void CustomAlphabet_AppliesToTransform()
    {
        var vm = CreateViewModel(out _, out _);
        vm.CustomAlphabet = "ZYXWVUTSRQPONMLKJIHGFEDCBA";
        vm.Keyword = "KEY";
        vm.InputText = "GEOCACHE";

        var expected = new BeaufortCipher("ZYXWVUTSRQPONMLKJIHGFEDCBA").Encrypt("GEOCACHE", "KEY");
        Assert.AreEqual(expected, vm.OutputText);
    }

    [TestMethod]
    public void InvalidCustomAlphabet_ShowsWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "KEY";
        vm.InputText = "HELLO";
        Assert.IsTrue(vm.HasOutput);

        vm.CustomAlphabet = "AAB"; // duplicate letter
        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Copy_PutsOutputOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.Keyword = "KEY";
        vm.InputText = "HELLO";

        vm.CopyOutputCommand.Execute(null);
        Assert.AreEqual(vm.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public void Clear_ResetsInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.Keyword = "KEY";
        vm.InputText = "HELLO";

        vm.ClearCommand.Execute(null);
        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void TableauRows_ArePopulatedWith26Rows()
    {
        var vm = CreateViewModel(out _, out _);
        Assert.AreEqual(26, vm.TableauRows.Count);
        Assert.AreEqual("A", vm.TableauRows[0].Header);
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", vm.TableauRows[0].Letters);
    }

    /// <summary>Minimal catalog that returns one descriptor so the base VM can activate.</summary>
    private sealed class SingleToolCatalog(ToolDescriptor descriptor) : ICatalogService
    {
        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => [descriptor];

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => [descriptor];

        public IReadOnlyList<ToolDescriptor> Search(string query) => [descriptor];
    }
}
