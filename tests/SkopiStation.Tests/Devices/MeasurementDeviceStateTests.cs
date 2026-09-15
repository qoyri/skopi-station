using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkopiStation.Devices;

namespace SkopiStation.Tests.Devices;

public class MeasurementDeviceStateTests
{
    [Fact]
    public async Task Disconnecting_twice_reports_the_state_once()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        await device.ConnectAsync("FAKE", CancellationToken.None);

        var states = new List<DeviceState>();
        device.StateChanged += (_, e) => states.Add(e.State);

        await device.DisconnectAsync();
        await device.DisconnectAsync();

        states.Should().Equal(DeviceState.Disconnected);
    }

    [Fact]
    public async Task Disposing_after_a_disconnect_reports_nothing_further()
    {
        var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        await device.ConnectAsync("FAKE", CancellationToken.None);
        await device.DisconnectAsync();

        var states = new List<DeviceState>();
        device.StateChanged += (_, e) => states.Add(e.State);

        await device.DisposeAsync();

        states.Should().BeEmpty();
    }

    [Fact]
    public async Task Reconnecting_to_another_port_reports_the_new_port()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        var messages = new List<string?>();
        device.StateChanged += (_, e) => messages.Add(e.Message);

        await device.ConnectAsync("FAKE1", CancellationToken.None);
        await device.DisconnectAsync();
        await device.ConnectAsync("FAKE2", CancellationToken.None);

        messages.Should().Contain("FAKE1").And.Contain("FAKE2");
    }
}
