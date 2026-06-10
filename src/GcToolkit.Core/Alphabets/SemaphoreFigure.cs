namespace GcToolkit.Core.Alphabets;

/// <summary>What a semaphore figure stands for.</summary>
public enum SemaphoreFigureKind
{
    Letter,
    Space,
    LettersSign,
    NumbersSign,
}

/// <summary>
/// One semaphore figure: two arm positions (signaller's perspective) plus what it stands for.
/// <see cref="Letter"/> is <c>'A'</c>–<c>'Z'</c> for letters and <c>'\0'</c> for the special signs;
/// <see cref="Digit"/> is set for the ten letters that double as digits (A=1 … I=9, K=0).
/// </summary>
public sealed record SemaphoreFigure(
    SemaphoreFigureKind Kind,
    char Letter,
    char? Digit,
    SemaphoreArmPosition LeftArm,
    SemaphoreArmPosition RightArm)
{
    /// <summary>Left-flag rotation for a viewer-facing rendering, in degrees clockwise from straight
    /// down. The signaller faces the viewer, so the left arm renders on the viewer's right.</summary>
    public double LeftFlagAngle => LeftArm switch
    {
        SemaphoreArmPosition.Down => 0,
        SemaphoreArmPosition.Low => 315,
        SemaphoreArmPosition.Out => 270,
        SemaphoreArmPosition.High => 225,
        SemaphoreArmPosition.Up => 180,
        SemaphoreArmPosition.AcrossLow => 45,
        _ => 135, // AcrossHigh
    };

    /// <summary>Right-flag rotation for a viewer-facing rendering (renders on the viewer's left).</summary>
    public double RightFlagAngle => RightArm switch
    {
        SemaphoreArmPosition.Down => 0,
        SemaphoreArmPosition.Low => 45,
        SemaphoreArmPosition.Out => 90,
        SemaphoreArmPosition.High => 135,
        SemaphoreArmPosition.Up => 180,
        SemaphoreArmPosition.AcrossLow => 315,
        _ => 225, // AcrossHigh
    };
}
