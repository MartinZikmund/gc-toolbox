namespace GcToolkit.Core.Alphabets;

/// <summary>The physical keyboard layouts the <see cref="DvorakCodec"/> can remap between.</summary>
public enum DvorakLayout
{
    /// <summary>The standard US QWERTY layout (the reference position order).</summary>
    Qwerty,

    /// <summary>The standard two-handed Simplified Dvorak layout.</summary>
    DvorakTwoHands,

    /// <summary>The one-handed right-hand Dvorak layout.</summary>
    DvorakRightHand,

    /// <summary>The one-handed left-hand Dvorak layout.</summary>
    DvorakLeftHand,
}
