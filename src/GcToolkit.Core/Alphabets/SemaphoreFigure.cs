namespace GcToolkit.Core.Alphabets;

/// <summary>What a semaphore figure stands for.</summary>
public enum SemaphoreFigureKind
{
    Letter,
    Space,
    LettersSign,
    NumbersSign,
    Cancel,
    Error,
}

/// <summary>
/// One semaphore figure: two arm positions (signaller's perspective) plus what it stands for.
/// <see cref="Letter"/> is <c>'A'</c>–<c>'Z'</c> for letters and <c>'\0'</c> for the special signs;
/// <see cref="Digit"/> is set for the ten letters that double as digits (A=1 … I=9, K=0). The optional
/// <see cref="SecondaryLeftArm"/>/<see cref="SecondaryRightArm"/> give a figure a second pose so a
/// waving signal (e.g. Error) can be drawn as motion between two arm positions.
/// </summary>
public sealed record SemaphoreFigure(
    SemaphoreFigureKind Kind,
    char Letter,
    char? Digit,
    SemaphoreArmPosition LeftArm,
    SemaphoreArmPosition RightArm,
    SemaphoreArmPosition? SecondaryLeftArm = null,
    SemaphoreArmPosition? SecondaryRightArm = null)
{
    /// <summary>Left-flag rotation for a viewer-facing rendering, in degrees clockwise from straight
    /// down. The signaller faces the viewer, so the left arm renders on the viewer's right.</summary>
    public double LeftFlagAngle => LeftFlagAngleFor(LeftArm);

    /// <summary>Right-flag rotation for a viewer-facing rendering (renders on the viewer's left).</summary>
    public double RightFlagAngle => RightFlagAngleFor(RightArm);

    /// <summary>The second (waving) pose's left-flag angle, or <see langword="null"/> when the figure is static.</summary>
    public double? SecondaryLeftFlagAngle => SecondaryLeftArm is { } arm ? LeftFlagAngleFor(arm) : null;

    /// <summary>The second (waving) pose's right-flag angle, or <see langword="null"/> when the figure is static.</summary>
    public double? SecondaryRightFlagAngle => SecondaryRightArm is { } arm ? RightFlagAngleFor(arm) : null;

    private static double LeftFlagAngleFor(SemaphoreArmPosition arm) => arm switch
    {
        SemaphoreArmPosition.Down => 0,
        SemaphoreArmPosition.Low => 315,
        SemaphoreArmPosition.Out => 270,
        SemaphoreArmPosition.High => 225,
        SemaphoreArmPosition.Up => 180,
        SemaphoreArmPosition.AcrossLow => 45,
        _ => 135, // AcrossHigh
    };

    private static double RightFlagAngleFor(SemaphoreArmPosition arm) => arm switch
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
