namespace GcToolkit.Core.Numbers;

/// <summary>One prime in a factorization, with its exponent (e.g. 2³ → Prime 2, Exponent 3).</summary>
public readonly record struct PrimeFactor(ulong Prime, int Exponent);
