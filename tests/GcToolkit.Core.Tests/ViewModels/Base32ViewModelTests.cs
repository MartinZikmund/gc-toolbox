using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class Base32ViewModelTests
{
    private static Base32ViewModel CreateViewModel(out FakeClipboardService clipboard, out FakeShareService share)
    {
        CatalogService catalog = new(
            [new StubCategoryContributor(new Category("Numbers", "Numbers_Name", 0, "Numbers"))],
            [new StubToolContributor(new ToolDescriptor("Base32", "Base32_Name", "Numbers", new string[0], "Base32", typeof(object), false, "Base32_Tooltip"))],
            new ToolMatcher(),
            new FakeStringLocalizer());

        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "Base32_Name", "Base32" },
            { "Base32_Tooltip", "Encode and decode." },
            { "Base32InvalidCharFormat", "Invalid character '{0}' at position {1}." },
            { "Base32CustomAlphabetInvalid", "Custom alphabet must have 32 distinct symbols." },
        });

        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        Base32ViewModel vm = new(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Encode_LiveAsPlainTextChanges_ProducesBase32()
    {
        var vm = CreateViewModel(out _, out _);

        vm.PlainText = "foobar";

        Assert.AreEqual("MZXW6YTBOI======", vm.Base32Text);
        Assert.IsTrue(vm.HasOutput);
        Assert.AreEqual(6, vm.ByteCount);
        Assert.AreEqual("66 6F 6F 62 61 72", vm.HexView);
    }

    [TestMethod]
    public void Encode_WithoutPadding_OmitsEquals()
    {
        var vm = CreateViewModel(out _, out _);

        vm.UsePadding = false;
        vm.PlainText = "f";

        Assert.AreEqual("MY", vm.Base32Text);
    }

    [TestMethod]
    public void Encode_GroupOutput_InsertsSeparators()
    {
        var vm = CreateViewModel(out _, out _);

        vm.UsePadding = false;
        vm.GroupSize = 4;
        vm.GroupOutput = true;
        vm.PlainText = "foobar";

        Assert.AreEqual("MZXW 6YTB OI", vm.Base32Text);
    }

    [TestMethod]
    public void Decode_LiveAsBase32Changes_ProducesPlainText()
    {
        var vm = CreateViewModel(out _, out _);

        vm.DirectionIndex = 1; // decode
        vm.Base32Text = "MZXW6YTBOI======";

        Assert.AreEqual("foobar", vm.PlainText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Decode_LenientLowercaseAndSpaces_StillWorks()
    {
        var vm = CreateViewModel(out _, out _);

        vm.DirectionIndex = 1;
        vm.Base32Text = "mzxw 6yt boi";

        Assert.AreEqual("foobar", vm.PlainText);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Decode_InvalidChar_SetsErrorWithPosition()
    {
        var vm = CreateViewModel(out _, out _);

        vm.DirectionIndex = 1;
        vm.Base32Text = "MZ1XW6"; // '1' is invalid in RFC 4648

        Assert.IsTrue(vm.HasError);
        StringAssert.Contains(vm.ErrorMessage, "'1'");
        StringAssert.Contains(vm.ErrorMessage, "2");
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.PlainText);
    }

    [TestMethod]
    public void Decode_HexEncoding_RoundTripsRawBytes()
    {
        var vm = CreateViewModel(out _, out _);

        // base32hex variant decode of "foobar".
        vm.VariantIndex = 1; // base32hex
        vm.DirectionIndex = 1;
        vm.Base32Text = "CPNMUOJ1E8======";

        Assert.AreEqual("foobar", vm.PlainText);
    }

    [TestMethod]
    public void Swap_FlipsDirectionAndConvertsBack()
    {
        var vm = CreateViewModel(out _, out _);

        vm.UsePadding = false;
        vm.PlainText = "foo";
        Assert.AreEqual("MZXW6", vm.Base32Text);

        vm.SwapCommand.Execute(null); // now decode the Base32 back into plaintext

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual("foo", vm.PlainText);
    }

    [TestMethod]
    public void Example_LoadsWorkedSample()
    {
        var vm = CreateViewModel(out _, out _);

        vm.ExampleCommand.Execute(null);

        Assert.AreEqual("geocaching", vm.PlainText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(string.IsNullOrEmpty(vm.Base32Text));
    }

    [TestMethod]
    public void Clear_EmptiesBothSides()
    {
        var vm = CreateViewModel(out _, out _);
        vm.PlainText = "foobar";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.PlainText);
        Assert.AreEqual(string.Empty, vm.Base32Text);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void CopyOutput_Encoding_CopiesUngroupedBase32()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.GroupOutput = true;
        vm.PlainText = "foobar";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("MZXW6YTBOI======", clipboard.LastText);
    }

    [TestMethod]
    public void ShareOutput_Decoding_SharesPlainText()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.DirectionIndex = 1;
        vm.Base32Text = "MZXW6YTBOI======";

        vm.ShareOutputCommand.Execute(null);

        Assert.AreEqual(1, share.ShareTextCallCount);
        Assert.AreEqual("foobar", share.LastText);
    }

    [TestMethod]
    public void IsCustomVariant_TrueOnlyForLastIndex()
    {
        var vm = CreateViewModel(out _, out _);
        Assert.IsFalse(vm.IsCustomVariant);

        vm.VariantIndex = vm.Variants.Count - 1;

        Assert.IsTrue(vm.IsCustomVariant);
    }

    [TestMethod]
    public void CustomAlphabet_InvalidLength_ShowsError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.VariantIndex = vm.Variants.Count - 1; // custom
        vm.CustomAlphabet = "TOOSHORT";

        vm.PlainText = "foobar";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Decode_PopulatesTryAllVariantsSolver()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1;
        vm.Base32Text = "MZXW6YTBOI======";

        Assert.AreEqual(4, vm.AllVariantResults.Count);
        Assert.IsTrue(vm.AllVariantResults[0].Success);
        Assert.IsTrue(vm.ShowSolver);
    }

    [TestMethod]
    public void Encoding_HidesSolver()
    {
        var vm = CreateViewModel(out _, out _);
        vm.PlainText = "foobar"; // encode direction — solver is decode-only

        Assert.IsFalse(vm.ShowSolver);
        Assert.AreEqual(0, vm.AllVariantResults.Count);
    }

    [TestMethod]
    public void EmptyInput_ClearsOutputWithoutError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.PlainText = "foo";
        Assert.IsTrue(vm.HasOutput);

        vm.PlainText = string.Empty;

        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(string.Empty, vm.Base32Text);
    }
}
