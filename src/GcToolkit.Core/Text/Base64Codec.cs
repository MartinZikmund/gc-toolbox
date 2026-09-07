using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>Why a Base64 decode attempt failed.</summary>
public enum Base64DecodeError
{
    None,

    /// <summary>A character outside the Base64 alphabet survived normalization (or data followed the padding).</summary>
    InvalidCharacter,

    /// <summary>The payload has a single leftover character in its last group — no padding can rescue it.</summary>
    InvalidLength,
}

/// <summary>
/// The outcome of one decode attempt. Never throws, so the UI can render a message instead of a crash.
/// </summary>
/// <param name="Success">Whether the input could be decoded at all.</param>
/// <param name="Text">The decoded text (empty on failure).</param>
/// <param name="ByteCount">How many bytes the payload carried.</param>
/// <param name="Error">The failure reason, or <see cref="Base64DecodeError.None"/>.</param>
/// <param name="InvalidCharacter">The offending character for <see cref="Base64DecodeError.InvalidCharacter"/>.</param>
/// <param name="IsBinary">The bytes are not valid UTF-8 — <see cref="Text"/> is their Latin-1 rendering.</param>
/// <param name="WasRepaired">The input was not canonical Base64: whitespace, padding or URL-safe characters were fixed up.</param>
public readonly record struct Base64DecodeResult(
    bool Success,
    string Text,
    int ByteCount,
    Base64DecodeError Error,
    string InvalidCharacter,
    bool IsBinary,
    bool WasRepaired)
{
    public static Base64DecodeResult Ok(string text, int byteCount, bool isBinary, bool wasRepaired)
        => new(true, text, byteCount, Base64DecodeError.None, string.Empty, isBinary, wasRepaired);

    public static Base64DecodeResult Fail(Base64DecodeError error, char invalidCharacter = '\0')
        => new(false, string.Empty, 0, error, invalidCharacter == '\0' ? string.Empty : invalidCharacter.ToString(), false, false);
}

/// <summary>
/// A pure, forgiving Base64 codec — the single source of truth for the Base64 tool. Encoding is
/// strict RFC 4648 over the text's UTF-8 bytes (standard <c>A–Z a–z 0–9 + /</c> alphabet, optional
/// <c>=</c> padding). Decoding is deliberately lenient, because Base64 found in a cache listing has
/// usually been through a forum, a PDF or an email: embedded whitespace and line breaks are dropped,
/// missing or surplus padding is rebuilt, and the URL-safe <c>-</c>/<c>_</c> aliases are accepted.
/// Genuinely broken input is reported, never thrown.
/// </summary>
public sealed class Base64Codec
{
    /// <summary>Shortest payload the auto-detector will consider — below this, false positives dominate.</summary>
    private const int MinimumDetectableLength = 4;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Encodes <paramref name="text"/>'s UTF-8 bytes, optionally dropping the <c>=</c> padding.</summary>
    public string Encode(string? text, bool includePadding = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        return includePadding ? encoded : encoded.TrimEnd('=');
    }

    /// <summary>Decodes <paramref name="input"/> leniently. Empty or padding-only input succeeds with empty text.</summary>
    public Base64DecodeResult Decode(string? input) => DecodeCore(input);

    /// <summary>
    /// Whether <paramref name="input"/> should be treated as Base64 when the direction is left on auto.
    /// Deliberately conservative: the payload must decode to non-empty, valid UTF-8 text with no
    /// control characters, so ordinary words that happen to use only Base64 letters stay plain text.
    /// </summary>
    public static bool LooksLikeBase64(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || CountPayloadCharacters(input) < MinimumDetectableLength)
        {
            return false;
        }

        var result = DecodeCore(input);
        return result is { Success: true, IsBinary: false, Text.Length: > 0 }
            && !result.Text.Any(IsUnexpectedControl);
    }

    private static Base64DecodeResult DecodeCore(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return Base64DecodeResult.Ok(string.Empty, 0, isBinary: false, wasRepaired: false);
        }

        var payload = new StringBuilder(input.Length);
        var sawPadding = false;

        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c == '=')
            {
                sawPadding = true;
                continue;
            }

            // Padding closes the payload; anything after it is corruption, not sloppiness.
            if (sawPadding)
            {
                return Base64DecodeResult.Fail(Base64DecodeError.InvalidCharacter, c);
            }

            var mapped = c switch
            {
                '-' => '+',
                '_' => '/',
                _ => c,
            };

            if (!IsBase64Character(mapped))
            {
                return Base64DecodeResult.Fail(Base64DecodeError.InvalidCharacter, c);
            }

            payload.Append(mapped);
        }

        // A single leftover character carries 6 bits — never a whole byte, so no padding can fix it.
        if (payload.Length % 4 == 1)
        {
            return Base64DecodeResult.Fail(Base64DecodeError.InvalidLength);
        }

        payload.Append('=', (4 - (payload.Length % 4)) % 4);
        var canonical = payload.ToString();
        var wasRepaired = !string.Equals(canonical, input, StringComparison.Ordinal);

        var bytes = new byte[canonical.Length / 4 * 3];
        if (!Convert.TryFromBase64String(canonical, bytes, out var written))
        {
            // Unreachable: every character was validated above. Reported rather than thrown.
            return Base64DecodeResult.Fail(Base64DecodeError.InvalidCharacter);
        }

        var payloadBytes = bytes.AsSpan(0, written);

        try
        {
            return Base64DecodeResult.Ok(StrictUtf8.GetString(payloadBytes), written, isBinary: false, wasRepaired);
        }
        catch (DecoderFallbackException)
        {
            // Base64 of arbitrary bytes is common in puzzles — show them byte for byte instead of failing.
            return Base64DecodeResult.Ok(Encoding.Latin1.GetString(payloadBytes), written, isBinary: true, wasRepaired);
        }
    }

    private static int CountPayloadCharacters(string input)
        => input.Count(static c => !char.IsWhiteSpace(c) && c != '=');

    private static bool IsBase64Character(char c)
        => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/';

    /// <summary>Tabs and newlines are legitimate in decoded text; any other control character is not.</summary>
    private static bool IsUnexpectedControl(char c) => char.IsControl(c) && c is not ('\t' or '\r' or '\n');
}
