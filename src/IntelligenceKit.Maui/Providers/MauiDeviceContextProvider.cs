using IntelligenceKit.Core.Providers;

namespace IntelligenceKit.Maui.Providers;

public class MauiDeviceContextProvider : IDeviceContextProvider
{
    public string Platform => DeviceInfo.Current.Platform.ToString();

    public string DeviceName => DeviceInfo.Current.Name;

    public string Model => DeviceInfo.Current.Model;

    public string Manufacturer => DeviceInfo.Current.Manufacturer;

    public string OperatingSystem => DeviceInfo.Current.VersionString;
}
