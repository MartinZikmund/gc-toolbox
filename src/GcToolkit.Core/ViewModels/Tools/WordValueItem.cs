namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row of the per-word breakdown: the <see cref="Word"/> and its letter-sum <see cref="Value"/>.</summary>
public sealed record WordValueItem(string Word, int Value);
