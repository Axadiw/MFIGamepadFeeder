using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Backends;

public enum BackendPreference
{
    Auto = 0,
    ViGEmBus = 1,
    ScpVBus = 2,
}

public interface IVirtualController : IDisposable
{
    string BackendName { get; }
    uint SlotIndex { get; }

    /// <summary>
    /// Submits the complete Xbox 360 state atomically in a single bus report.
    /// </summary>
    void SubmitState(in GamepadState state);

    /// <summary>
    /// Resets all buttons, triggers, and axes to neutral.
    /// </summary>
    void Reset();
}

public interface IVirtualGamepadBackend : IDisposable
{
    string Name { get; }
    bool IsLegacyDriver { get; }
    bool IsAvailable();
    IVirtualController ConnectController(uint slotIndex);
}

public sealed record BackendProbeResult(
    bool ViGEmAvailable,
    bool ScpVBusAvailable,
    string? ScpVBusDevicePath,
    string Summary);
