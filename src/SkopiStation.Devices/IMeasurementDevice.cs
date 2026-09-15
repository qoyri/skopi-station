namespace SkopiStation.Devices;

public enum DeviceState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted,
}

public sealed class MeasurementReceivedEventArgs(MeasurementFrame frame) : EventArgs
{
    public MeasurementFrame Frame { get; } = frame;
}

public sealed class DeviceStateChangedEventArgs(DeviceState state, string? message = null) : EventArgs
{
    public DeviceState State { get; } = state;

    public string? Message { get; } = message;
}

/// <summary>
/// A source of measurements. Implemented once over a serial port and once in memory, so the
/// application can be demonstrated and tested without any hardware attached.
/// </summary>
/// <remarks>
/// Both events are raised from a background thread. Consumers are responsible for marshalling to
/// the UI thread; nothing in this namespace knows about a dispatcher.
/// </remarks>
public interface IMeasurementDevice
{
    event EventHandler<MeasurementReceivedEventArgs> MeasurementReceived;

    event EventHandler<DeviceStateChangedEventArgs> StateChanged;

    Task ConnectAsync(string portName, CancellationToken ct);

    Task DisconnectAsync();
}
