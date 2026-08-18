using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Providers;

/// <summary>
/// Captures a live snapshot of the device's runtime state (memory, battery,
/// network, current screen). Separate from <see cref="IDeviceContextProvider"/>,
/// which describes the static device; this one changes moment to moment.
/// </summary>
public interface IRuntimeContextProvider
{
    DeviceRuntime Capture();
}
