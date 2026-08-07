namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One line of the Vigenère running-key display: the <see cref="KeyLetter"/> and the
/// <see cref="CipherAlphabet"/> (the plain alphabet rotated by that letter), shown beneath the shared
/// <see cref="PlainAlphabet"/> so the per-position substitution is visible at a glance.
/// </summary>
public sealed class VigenereKeyRow(char keyLetter, string plainAlphabet, string cipherAlphabet)
{
    public string KeyLetter { get; } = keyLetter.ToString();

    public string PlainAlphabet { get; } = plainAlphabet;

    public string CipherAlphabet { get; } = cipherAlphabet;

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{KeyLetter}: {CipherAlphabet}";
}
