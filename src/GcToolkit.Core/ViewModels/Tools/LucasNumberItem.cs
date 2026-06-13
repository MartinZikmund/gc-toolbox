namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row in a Lucas-numbers result list: the index, the value formatted with the current
/// culture's thousands separators, and the value's base-10 digit count.</summary>
public sealed record LucasNumberItem(int Index, string Value, int DigitCount);
