namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One of the eight directions a semaphore arm/flag can point, expressed from the signaller's own
/// perspective. <c>Across*</c> positions reach over the body to the opposite side.
/// </summary>
public enum SemaphoreArmPosition
{
    Down,
    Low,
    Out,
    High,
    Up,
    AcrossLow,
    AcrossHigh,
}
