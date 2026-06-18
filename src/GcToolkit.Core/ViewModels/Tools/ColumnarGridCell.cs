namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One cell of the live columnar grid preview: a single character (or empty for an unfilled trailing
/// cell of an irregular grid). Items are immutable — the ViewModel rebuilds the grid when the input,
/// key, or padding changes.
/// </summary>
public sealed class ColumnarGridCell
{
    public ColumnarGridCell(int row, int column, char? character)
    {
        Row = row;
        Column = column;
        Glyph = character?.ToString() ?? string.Empty;
        IsEmpty = character is null;
    }

    public int Row { get; }

    public int Column { get; }

    /// <summary>The cell's character as a string, or empty for an unfilled cell.</summary>
    public string Glyph { get; }

    public bool IsEmpty { get; }
}

/// <summary>
/// One column header of the grid preview: the keyword letter (or digit) shown above the column and
/// its resolved 1-based read order.
/// </summary>
public sealed class ColumnarHeaderCell(char letter, int order)
{
    public string Letter { get; } = letter.ToString();

    public int Order { get; } = order;

    public string OrderText { get; } = order.ToString(System.Globalization.CultureInfo.CurrentCulture);
}
