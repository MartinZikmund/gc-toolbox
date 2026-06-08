namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row of the "count separate words" breakdown: the <see cref="Word"/> and its rendered
/// <see cref="Detail"/> line (calculation and/or reduction chain, e.g. <c>19 + 4 + 6 = 29 = 11 = 2</c>).</summary>
public sealed record WordValueWordItem(string Word, string Detail);

/// <summary>One cell of the conversion table: a scored <see cref="Character"/> and its <see cref="Value"/>.</summary>
public sealed record WordValueConversionItem(string Character, int Value);
