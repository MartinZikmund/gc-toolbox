using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>Which side the user is editing (and therefore which direction the live preview runs).</summary>
public enum Base32Direction
{
    /// <summary>Plaintext is the source — encode it into Base32.</summary>
    Encode,

    /// <summary>Base32 is the source — decode it back into plaintext.</summary>
    Decode,
}

/// <summary>The text encoding applied between the plaintext string and its raw bytes.</summary>
public enum Base32TextEncoding
{
    Utf8,
    Ascii,
    Latin1,
}

/// <summary>One row in the "try all variants" solver panel.</summary>
public sealed record Base32VariantPreview(string VariantName, bool Success, string Preview);

/// <summary>
/// Base32 encoder / decoder (issue #119). Provides a live, two-way plaintext ↔ Base32 preview with a
/// one-tap swap, multiple alphabets (RFC 4648 / base32hex / Crockford / z-base-32 + custom), a padding
/// toggle, lenient decoding with first-invalid-character reporting, selectable plaintext encoding
/// (UTF-8 / ASCII / Latin-1) with a hex + byte-count view, output grouping, an auto-detect / try-all
/// solver, and copy / share / clear — all offline. Logic lives in <see cref="Base32Codec"/>.
/// </summary>
[Tool("Base32", ToolCategory.Numbers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["base32", "base-32", "rfc 4648", "base32hex", "crockford", "z-base-32", "encode", "decode", "kódování"])]
public sealed partial class Base32ViewModel : ToolViewModelBase
{
    private const string ExamplePlaintext = "geocaching";

    private readonly Base32Codec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public Base32ViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Base32", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = encode (plaintext → Base32), 1 = decode (Base32 → plaintext).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>The plaintext side. The source when encoding; the result when decoding.</summary>
    [ObservableProperty]
    public partial string PlainText { get; set; } = string.Empty;

    /// <summary>The Base32 side. The source when decoding; the result when encoding.</summary>
    [ObservableProperty]
    public partial string Base32Text { get; set; } = string.Empty;

    /// <summary>Index into <see cref="Variants"/> (RFC 4648 / base32hex / Crockford / z-base-32 / custom).</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>Index into <see cref="TextEncodings"/> (UTF-8 / ASCII / Latin-1).</summary>
    [ObservableProperty]
    public partial int TextEncodingIndex { get; set; }

    /// <summary>Whether encoding emits <c>=</c> padding (defaults to the variant's convention).</summary>
    [ObservableProperty]
    public partial bool UsePadding { get; set; } = true;

    /// <summary>Group the Base32 output every <see cref="GroupSize"/> chars with a space for readability.</summary>
    [ObservableProperty]
    public partial bool GroupOutput { get; set; }

    [ObservableProperty]
    public partial int GroupSize { get; set; } = 4;

    /// <summary>The custom 32-symbol alphabet, used when the custom variant is selected.</summary>
    [ObservableProperty]
    public partial string CustomAlphabet { get; set; } = Base32Codec.AlphabetFor(Base32Variant.Rfc4648);

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Hex dump of the decoded/encoded bytes (e.g. <c>"66 6F 6F"</c>).</summary>
    [ObservableProperty]
    public partial string HexView { get; set; } = string.Empty;

    /// <summary>Number of raw bytes represented by the current conversion.</summary>
    [ObservableProperty]
    public partial int ByteCount { get; set; }

    /// <summary><see langword="true"/> when the "try all variants" solver panel has rows to show.</summary>
    [ObservableProperty]
    public partial bool ShowSolver { get; set; }

    /// <summary>Whether the custom-alphabet field is shown and used.</summary>
    public bool IsCustomVariant => VariantIndex == Variants.Count - 1;

    /// <summary>Localized variant names for the picker (built-ins + a "Custom" entry).</summary>
    public ObservableCollection<string> Variants { get; } = [];

    /// <summary>Localized text-encoding names for the picker.</summary>
    public ObservableCollection<string> TextEncodings { get; } = [];

    /// <summary>Per-variant decode preview rows for the "try all variants" solver.</summary>
    public ObservableCollection<Base32VariantPreview> AllVariantResults { get; } = [];

    private bool IsEncoding => DirectionIndex != 1;

    public override void ViewCreated()
    {
        base.ViewCreated();

        // Populate the pickers once names can be localized (after the base resolves the tool).
        if (Variants.Count == 0)
        {
            foreach (var variant in Base32Codec.AllVariants)
            {
                Variants.Add(_localizer[$"Base32Variant_{variant}"].Value);
            }

            Variants.Add(_localizer["Base32Variant_Custom"].Value);

            TextEncodings.Add(_localizer["Base32Encoding_Utf8"].Value);
            TextEncodings.Add(_localizer["Base32Encoding_Ascii"].Value);
            TextEncodings.Add(_localizer["Base32Encoding_Latin1"].Value);
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        Convert();
    }

    partial void OnPlainTextChanged(string value)
    {
        if (!_suppressConvert && IsEncoding)
        {
            Convert();
        }
    }

    partial void OnBase32TextChanged(string value)
    {
        if (!_suppressConvert && !IsEncoding)
        {
            Convert();
        }
    }

    partial void OnVariantIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomVariant));

        // Adopt the new variant's padding convention unless the user is on the custom alphabet.
        if (!IsCustomVariant && value >= 0 && value < Base32Codec.AllVariants.Count)
        {
            _suppressConvert = true;
            UsePadding = Base32Codec.DefaultPadding(Base32Codec.AllVariants[value]);
            _suppressConvert = false;
        }

        Convert();
    }

    partial void OnTextEncodingIndexChanged(int value) => Convert();

    partial void OnUsePaddingChanged(bool value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnGroupOutputChanged(bool value) => Convert();

    partial void OnGroupSizeChanged(int value) => Convert();

    partial void OnCustomAlphabetChanged(string value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Runs the active direction, refreshing output, byte stats, errors and the solver panel.</summary>
    public void Convert()
    {
        ClearStatus();

        if (IsEncoding)
        {
            EncodeDirection();
        }
        else
        {
            DecodeDirection();
        }

        RefreshAllVariants();
    }

    private void EncodeDirection()
    {
        if (string.IsNullOrEmpty(PlainText))
        {
            _suppressConvert = true;
            Base32Text = string.Empty;
            _suppressConvert = false;
            SetResult(string.Empty, []);
            return;
        }

        var bytes = SelectedEncoding().GetBytes(PlainText);
        var encoded = EncodeBytes(bytes);

        _suppressConvert = true;
        Base32Text = GroupOutput ? Group(encoded) : encoded;
        _suppressConvert = false;

        SetResult(Base32Text, bytes);
    }

    private void DecodeDirection()
    {
        if (string.IsNullOrWhiteSpace(Base32Text))
        {
            _suppressConvert = true;
            PlainText = string.Empty;
            _suppressConvert = false;
            SetResult(string.Empty, []);
            return;
        }

        bool success;
        byte[] bytes;
        Base32DecodeError? error;
        if (IsCustomVariant)
        {
            if (!Base32Codec.IsValidCustomAlphabet(CustomAlphabet))
            {
                ShowError(_localizer["Base32CustomAlphabetInvalid"].Value);
                return;
            }

            success = _codec.TryDecodeCustom(Base32Text, CustomAlphabet, out bytes, out error);
        }
        else
        {
            success = _codec.TryDecode(Base32Text, SelectedVariant(), out bytes, out error, out _);
        }

        if (!success)
        {
            var message = error is { } e
                ? string.Format(_localizer["Base32InvalidCharFormat"].Value, e.InvalidChar, e.Position)
                : _localizer["Base32DecodeFailed"].Value;
            ShowError(message);
            return;
        }

        _suppressConvert = true;
        PlainText = Decode(bytes);
        _suppressConvert = false;

        SetResult(PlainText, bytes);
    }

    /// <summary>Encodes <paramref name="bytes"/> with the current variant / custom alphabet and padding.</summary>
    private string EncodeBytes(byte[] bytes)
    {
        if (IsCustomVariant)
        {
            if (!Base32Codec.IsValidCustomAlphabet(CustomAlphabet))
            {
                ShowError(_localizer["Base32CustomAlphabetInvalid"].Value);
                return string.Empty;
            }

            return _codec.EncodeCustom(bytes, CustomAlphabet, UsePadding);
        }

        return _codec.Encode(bytes, SelectedVariant(), UsePadding);
    }

    private void RefreshAllVariants()
    {
        AllVariantResults.Clear();

        // The solver only makes sense for decoding a Base32 blob.
        if (IsEncoding || string.IsNullOrWhiteSpace(Base32Text))
        {
            ShowSolver = false;
            return;
        }

        foreach (var result in _codec.TryAllVariants(Base32Text))
        {
            var name = _localizer[$"Base32Variant_{result.Variant}"].Value;
            var preview = result.Success
                ? Truncate(Decode(result.Bytes))
                : _localizer["Base32SolverInvalid"].Value;
            AllVariantResults.Add(new Base32VariantPreview(name, result.Success, preview));
        }

        ShowSolver = AllVariantResults.Count > 0;
    }

    private void SetResult(string output, byte[] bytes)
    {
        HasOutput = !string.IsNullOrEmpty(output);
        ByteCount = bytes.Length;
        HexView = bytes.Length == 0 ? string.Empty : System.Convert.ToHexString(bytes).Chunk(2).Select(c => new string(c)).Aggregate((a, b) => $"{a} {b}");
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        HasOutput = false;
        ByteCount = 0;
        HexView = string.Empty;

        // Wipe the stale result on the destination side so a bad input never shows an old answer.
        _suppressConvert = true;
        if (IsEncoding)
        {
            Base32Text = string.Empty;
        }
        else
        {
            PlainText = string.Empty;
        }

        _suppressConvert = false;
    }

    private void ClearStatus()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private Base32Variant SelectedVariant()
        => VariantIndex >= 0 && VariantIndex < Base32Codec.AllVariants.Count
            ? Base32Codec.AllVariants[VariantIndex]
            : Base32Variant.Rfc4648;

    private Encoding SelectedEncoding() => TextEncodingIndex switch
    {
        1 => Encoding.ASCII,
        2 => Encoding.Latin1,
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    private string Decode(byte[] bytes) => SelectedEncoding().GetString(bytes);

    private string Group(string text)
    {
        var size = GroupSize <= 0 ? 1 : GroupSize;
        if (text.Length <= size)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + text.Length / size);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && i % size == 0)
            {
                builder.Append(' ');
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static string Truncate(string text)
        => text.Length <= 40 ? text : text[..40] + "…";

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(IsEncoding ? StripGroups(Base32Text) : PlainText);

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, IsEncoding ? StripGroups(Base32Text) : PlainText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform failure must not crash the tool.
        }
    }

    /// <summary>Feeds the current result back as the next input by flipping direction — a one-tap swap.</summary>
    [RelayCommand]
    private void Swap() => DirectionIndex = IsEncoding ? 1 : 0;

    /// <summary>Loads a worked example so the tool is self-explaining on first open.</summary>
    [RelayCommand]
    private void Example()
    {
        _suppressConvert = true;
        DirectionIndex = 0;
        VariantIndex = 0;
        PlainText = ExamplePlaintext;
        _suppressConvert = false;
        Convert();
    }

    [RelayCommand]
    private void Clear()
    {
        _suppressConvert = true;
        PlainText = string.Empty;
        Base32Text = string.Empty;
        _suppressConvert = false;
        Convert();
    }

    private static string StripGroups(string text) => text.Replace(" ", string.Empty);
}
