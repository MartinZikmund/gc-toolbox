namespace GcToolkit.Core.Alphabets;

/// <summary>The four families of ICS flags.</summary>
public enum SignalFlagKind
{
    /// <summary>Square letter flag A–Z (A and B are swallowtailed).</summary>
    Letter,

    /// <summary>Tapered numeral pennant 0–9.</summary>
    Numeral,

    /// <summary>The answering (code/response) pennant.</summary>
    Answer,

    /// <summary>A triangular substitute (repeater) flag.</summary>
    Substitute,
}
