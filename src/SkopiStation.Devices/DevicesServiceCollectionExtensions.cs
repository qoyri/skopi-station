using Microsoft.Extensions.DependencyInjection;

namespace SkopiStation.Devices;

public enum DeviceMode
{
    Serial,
    Fake,
}

public static class DevicesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the measurement device. <see cref="DeviceMode.Fake"/> emits frames from memory,
    /// which is how the acquisition screen can be demonstrated without an instrument or a virtual
    /// port pair.
    /// </summary>
    public static IServiceCollection AddSkopiStationDevices(this IServiceCollection services, DeviceMode mode)
    {
        if (mode == DeviceMode.Fake)
        {
            services.AddSingleton<IMeasurementDevice, FakeMeasurementDevice>();
        }
        else
        {
            services.AddSingleton<IMeasurementDevice, SerialMeasurementDevice>();
        }

        return services;
    }
}
