namespace GcToolkit.Core.Services.Devices;

/// <summary>
/// A kind of device hardware a tool cannot work without. One member per hardware kind, not per tool.
/// A member may only be added when its presence can be answered synchronously, without prompting the
/// user and without side effects — see <see cref="IDeviceCapabilityService"/>. The camera torch does
/// NOT qualify (its Android probe enumerates cameras and, below API 23, opens one), so torch presence
/// lives on <c>ITorchService</c> instead; nothing is gated on it.
/// </summary>
public enum DeviceCapability
{
    Compass,
    // Geolocation,   // #371 Navigate to Coordinate — Geolocator presence is synchronous and does not prompt.
}
