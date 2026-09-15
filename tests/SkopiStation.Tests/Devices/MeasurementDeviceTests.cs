using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkopiStation.Devices;
using SkopiStation.Domain;

namespace SkopiStation.Tests.Devices;

/// <summary>
/// Exercises the reading loop over a text reader rather than a serial port. It is the same
/// <see cref="MeasurementDeviceBase.PumpAsync"/> the serial device runs, so the part of
/// <see cref="SerialMeasurementDevice"/> that carries the logic is covered without hardware; only
/// the opening of the port itself is not.
/// </summary>
internal sealed class PumpingDevice(ILogger logger) : MeasurementDeviceBase(logger)
{
    public override Task ConnectAsync(string portName, CancellationToken ct) => Task.CompletedTask;

    public override Task DisconnectAsync() => Task.CompletedTask;

    public Task ReadAllAsync(string payload, CancellationToken ct) =>
        PumpAsync(new StringReader(payload), ct);
}

public class MeasurementDeviceTests
{
    private const string ValidPressure = "MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z";
    private const string ValidLength = "MEAS|AxialLength|23.62|mm|2026-09-15T10:22:31Z";

    [Fact]
    public async Task Every_valid_frame_in_the_stream_is_published()
    {
        var device = new PumpingDevice(NullLogger.Instance);
        var received = new List<MeasurementFrame>();
        device.MeasurementReceived += (_, e) => received.Add(e.Frame);

        await device.ReadAllAsync($"{ValidPressure}\n{ValidLength}\n", CancellationToken.None);

        received.Should().HaveCount(2);
        received[0].Kind.Should().Be(MeasurementKind.IntraocularPressure);
        received[1].Kind.Should().Be(MeasurementKind.AxialLength);
    }

    /// <summary>
    /// The point of the rule: one corrupt frame must not take the stream, or the application,
    /// down with it.
    /// </summary>
    [Fact]
    public async Task A_malformed_frame_is_skipped_and_the_stream_carries_on()
    {
        var device = new PumpingDevice(NullLogger.Instance);
        var received = new List<MeasurementFrame>();
        device.MeasurementReceived += (_, e) => received.Add(e.Frame);

        var payload = string.Join(
            '\n',
            ValidPressure,
            "MEAS|IntraocularPressure|16.4|kPa|2026-09-15T10:22:31Z",
            "garbage",
            string.Empty,
            "MEAS|IntraocularPressure|-5|mmHg|2026-09-15T10:22:31Z",
            ValidLength);

        await device.ReadAllAsync(payload, CancellationToken.None);

        received.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_discarded_frame_is_logged_with_its_reason()
    {
        var logger = new RecordingLogger();
        var device = new PumpingDevice(logger);

        await device.ReadAllAsync(
            "MEAS|IntraocularPressure|16.4|kPa|2026-09-15T10:22:31Z\n",
            CancellationToken.None);

        logger.Entries.Should().ContainSingle()
            .Which.Should().Match<(LogLevel Level, string Message)>(entry =>
                entry.Level == LogLevel.Warning
                && entry.Message.Contains("kPa")
                && entry.Message.Contains("mmHg"));
    }

    [Fact]
    public async Task Cancelling_stops_the_loop_without_throwing()
    {
        var device = new PumpingDevice(NullLogger.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await device.ReadAllAsync($"{ValidPressure}\n", cancellation.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task The_fake_device_publishes_an_emitted_frame()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        var received = new List<MeasurementFrame>();
        device.MeasurementReceived += (_, e) => received.Add(e.Frame);

        device.Emit(ValidPressure);

        received.Should().ContainSingle().Which.Value.Should().Be(16.4m);
    }

    [Fact]
    public async Task The_fake_device_drops_a_malformed_frame_like_the_serial_one()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        var received = new List<MeasurementFrame>();
        device.MeasurementReceived += (_, e) => received.Add(e.Frame);

        device.Emit("MEAS|IntraocularPressure|16.4|kPa|2026-09-15T10:22:31Z");

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Connecting_and_disconnecting_the_fake_device_reports_its_state()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        var states = new List<DeviceState>();
        device.StateChanged += (_, e) => states.Add(e.State);

        await device.ConnectAsync("FAKE", CancellationToken.None);
        await device.DisconnectAsync();

        states.Should().Equal(DeviceState.Connecting, DeviceState.Connected, DeviceState.Disconnected);
    }

    /// <summary>
    /// Pins down the premise the acquisition ViewModel is built on: readings surface on a thread
    /// other than the one that asked for the connection. Were this to stop being true, the
    /// marshalling through IUiDispatcher would look like ceremony rather than a necessity.
    /// </summary>
    [Fact]
    public async Task Readings_surface_on_a_background_thread()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance)
        {
            Interval = TimeSpan.FromMilliseconds(10),
            Script = [ValidPressure],
        };

        var callingThread = Environment.CurrentManagedThreadId;
        var raisingThreads = new List<int>();
        device.MeasurementReceived += (_, _) => raisingThreads.Add(Environment.CurrentManagedThreadId);

        await device.ConnectAsync("FAKE", CancellationToken.None);
        await Task.Delay(200);
        await device.DisconnectAsync();

        raisingThreads.Should().NotBeEmpty();
        raisingThreads.Should().NotContain(callingThread);
    }

    [Fact]
    public async Task The_fake_device_emits_its_script_once_connected()
    {
        await using var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance)
        {
            Interval = TimeSpan.FromMilliseconds(10),
            Script = [ValidPressure, ValidLength],
        };

        var received = new List<MeasurementFrame>();
        device.MeasurementReceived += (_, e) => received.Add(e.Frame);

        await device.ConnectAsync("FAKE", CancellationToken.None);
        await Task.Delay(200);
        await device.DisconnectAsync();

        received.Should().NotBeEmpty();
        received.Should().OnlyContain(frame =>
            frame.Kind == MeasurementKind.IntraocularPressure || frame.Kind == MeasurementKind.AxialLength);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
