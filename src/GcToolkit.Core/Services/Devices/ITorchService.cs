using System;
using System.Threading;
using System.Threading.Tasks;

namespace GcToolkit.Core.Services.Devices;

/// <summary>
/// The camera torch. NEVER a visibility gate: Flashlight is always in the catalog and falls back to a
/// full-brightness screen. Presence is probed LAZILY, on the tool's first ask, never at startup —
/// Uno's Android probe enumerates cameras (and, below API 23, opens one), which has no business
/// running seconds after launch. Scoped per window; owns the lamp while lit.
/// </summary>
public interface ITorchService
{
    SensorStatus Status { get; }

    bool IsOn { get; }

    /// <summary>Whether hardware brightness is adjustable. False on Android (any non-zero level is full)
    /// and on flash-only iOS devices; true on iOS devices whose camera reports a torch.</summary>
    bool SupportsBrightness { get; }

    /// <summary>Raised when the lamp is taken or returned by another app, or permission changes.</summary>
    event EventHandler? StatusChanged;

    /// <summary>Probes the lamp once and caches the answer. Safe to await repeatedly.</summary>
    ValueTask<bool> GetIsSupportedAsync(CancellationToken cancellationToken = default);

    /// <summary>Turns the torch on/off. <paramref name="brightness"/> is 0..1 and is ignored where
    /// <see cref="SupportsBrightness"/> is false. Returns false on refusal; read <see cref="Status"/>.</summary>
    Task<bool> SetEnabledAsync(bool isOn, double brightness = 1.0, CancellationToken cancellationToken = default);

    /// <summary>Turns the torch off and disposes the underlying lamp. MUST be called from the tool's
    /// ViewUnloaded — Uno's iOS and Android Lamp hold unmanaged resources.</summary>
    void Release();
}
