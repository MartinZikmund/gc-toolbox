using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class VigenereCipherViewModelTests
{
    private const int EncodeMode = 0;
    private const int DecodeMode = 1;
    private const int AssistedMode = 2;

    private static VigenereCipherViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        return new VigenereCipherViewModel(
            new StubCatalogService("VigenereCipher"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard,
            share);
    }

    [TestMethod]
    public void Constructor_StartsEmpty_WithOutputActionsDisabled()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasKeyError, "an untouched page must not show a validation warning");
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(vm.ShareOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void InputText_WithKey_EncodesLive()
    {
        var vm = CreateViewModel(out _, out _);

        vm.KeyText = "LEMON";
        vm.InputText = "ATTACKATDAWN";

        Assert.AreEqual("LXFOPVEFRNHR", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void KeyText_Normalized_IsSurfacedAndDrivesTheKeyRows()
    {
        var vm = CreateViewModel(out _, out _);

        vm.KeyText = "l E-m.o N";

        Assert.AreEqual("LEMON", vm.NormalizedKey);
        Assert.AreEqual(5, vm.KeyRows.Count);
        Assert.IsTrue(vm.ShowKeyRows);
    }

    [TestMethod]
    public void KeyText_WithoutLetters_FlagsTheErrorAndSuppressesOutput()
    {
        var vm = CreateViewModel(out _, out _);

        vm.KeyText = "123!";
        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasKeyError);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void DirectionIndex_ToggledToDecode_CarriesTheResultIntoTheInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "LEMON";
        vm.InputText = "ATTACKATDAWN";

        vm.DirectionIndex = DecodeMode;

        // The one-tap round-trip: the ciphertext becomes the input and decodes back to the plaintext.
        Assert.AreEqual("LXFOPVEFRNHR", vm.InputText);
        Assert.AreEqual("ATTACKATDAWN", vm.OutputText);
    }

    [TestMethod]
    public void DirectionIndex_ToggledBackToEncode_RoundTrips()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "LEMON";
        vm.InputText = "ATTACKATDAWN";
        vm.DirectionIndex = DecodeMode;

        vm.DirectionIndex = EncodeMode;

        Assert.AreEqual("ATTACKATDAWN", vm.InputText);
        Assert.AreEqual("LXFOPVEFRNHR", vm.OutputText);
    }

    [TestMethod]
    public void DirectionIndex_SwitchedToAssisted_KeepsTheInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "LEMON";
        vm.InputText = "ATTACKATDAWN";

        vm.DirectionIndex = AssistedMode;

        Assert.AreEqual("ATTACKATDAWN", vm.InputText);
        Assert.IsTrue(vm.IsAssistedMode);
        Assert.IsFalse(vm.IsKeyedMode);
    }

    [TestMethod]
    public void AssistedMode_LongCiphertext_RecoversTheKeyAndPlaintext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = AssistedMode;

        vm.InputText = new Core.Ciphers.VigenereCipher().Encode(Prose, "LEMON");

        Assert.AreEqual("LEMON", vm.DerivedKey);
        Assert.IsTrue(vm.HasDerivedKey);
        Assert.AreEqual(Prose, vm.OutputText);
        Assert.IsTrue(vm.KeyLengthCandidates.Count > 0);
    }

    [TestMethod]
    public void AssistedMode_KeyLengthRange_ExcludesLengthsOutsideIt()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = AssistedMode;
        vm.MinKeyLength = 4;
        vm.MaxKeyLength = 8;

        vm.InputText = new Core.Ciphers.VigenereCipher().Encode(Prose, "LEMON");

        var lengths = vm.KeyLengthCandidates.Select(c => int.Parse(c.Length)).ToList();
        Assert.IsTrue(lengths.Count > 0);
        Assert.IsTrue(lengths.All(l => l is >= 4 and <= 8), $"got lengths [{string.Join(", ", lengths)}]");
    }

    [TestMethod]
    public void AssistedMode_SwitchedBackToKeyed_ClearsTheDerivedKeyAndCandidates()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = AssistedMode;
        vm.InputText = new Core.Ciphers.VigenereCipher().Encode(Prose, "LEMON");

        vm.DirectionIndex = DecodeMode;

        Assert.AreEqual(string.Empty, vm.DerivedKey);
        Assert.IsFalse(vm.HasDerivedKey);
        Assert.AreEqual(0, vm.KeyLengthCandidates.Count);
    }

    [TestMethod]
    public void CopyOutput_InAssistedMode_IncludesTheRecoveredKey()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.DirectionIndex = AssistedMode;
        vm.InputText = new Core.Ciphers.VigenereCipher().Encode(Prose, "LEMON");

        vm.CopyOutputCommand.Execute(null);

        Assert.IsTrue(clipboard.LastText!.StartsWith("LEMON", StringComparison.Ordinal));
        Assert.IsTrue(clipboard.LastText.Contains(Prose, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Clear_ResetsInputKeyAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "LEMON";
        vm.InputText = "ATTACKATDAWN";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.KeyText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasKeyError);
    }

    private const string Prose =
        "It is a truth universally acknowledged, that a single man in possession of a good fortune, "
        + "must be in want of a wife. However little known the feelings or views of such a man may be on "
        + "his first entering a neighbourhood, this truth is so well fixed in the minds of the surrounding "
        + "families, that he is considered the rightful property of some one or other of their daughters. "
        + "It was the best of times, it was the worst of times, it was the age of wisdom, it was the age of "
        + "foolishness, it was the epoch of belief, it was the epoch of incredulity, it was the season of "
        + "Light, it was the season of Darkness, it was the spring of hope, it was the winter of despair.";
}
