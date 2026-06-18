namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One cell of the rendered 5×5 Playfair key square: its <see cref="Letter"/>, 1-based
/// <see cref="Row"/>/<see cref="Column"/>, and a pre-built localized <see cref="AutomationName"/> for
/// screen readers (e.g. <c>"Row 1, column 2: L"</c>).</summary>
public readonly record struct PlayfairSquareCell(string Letter, int Row, int Column, string AutomationName);
