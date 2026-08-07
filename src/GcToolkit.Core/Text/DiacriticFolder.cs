using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>
/// Folds accented Latin letters to their plain ASCII base (é → e, ä → a, ñ → n, …) so they can be
/// scored by the basic A-Z schemes. Uses Unicode canonical decomposition to strip combining marks,
/// plus a small map for the letters that have no decomposition (ß, æ, ø, đ, ł and their uppercase).
/// </summary>
public static class DiacriticFolder
{
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // Expand / replace the non-decomposable letters first, then strip combining marks below.
        var expanded = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case 'ß': expanded.Append('s'); break;
                case 'ẞ': expanded.Append('S'); break;
                case 'æ': expanded.Append("ae"); break;
                case 'Æ': expanded.Append("AE"); break;
                case 'œ': expanded.Append("oe"); break;
                case 'Œ': expanded.Append("OE"); break;
                case 'ø': expanded.Append('o'); break;
                case 'Ø': expanded.Append('O'); break;
                case 'đ': expanded.Append('d'); break;
                case 'Đ': expanded.Append('D'); break;
                case 'ł': expanded.Append('l'); break;
                case 'Ł': expanded.Append('L'); break;
                default: expanded.Append(ch); break;
            }
        }

        var decomposed = expanded.ToString().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(ch);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
