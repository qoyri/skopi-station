using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace SkopiStation.Devices;

/// <summary>Reads measurement frames from a real serial port.</summary>
public sealed class SerialMeasurementDevice(ILogger<SerialMeasurementDevice> logger)
    : MeasurementDeviceBase(logger)
{
    private const int BaudRate = 9600;

    private SerialPort? port;
    private CancellationTokenSource? pumpCancellation;
    private Task? pump;

    /// <summary>The ports currently offered by the machine, for the connection drop-down.</summary>
    public static IReadOnlyList<string> AvailablePortNames() => SerialPort.GetPortNames();

    public override async Task ConnectAsync(string portName, CancellationToken ct)
    {
        await DisconnectAsync();

        SetState(DeviceState.Connecting, portName);

        try
        {
            ct.ThrowIfCancellationRequested();

            var opened = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
            {
                NewLine = "\n",
                Encoding = System.Text.Encoding.ASCII,
            };

            // Opening a serial port is a blocking call with no asynchronous counterpart, so it is
            // pushed off the calling thread rather than blocked on.
            await Task.Run(opened.Open, ct);

            port = opened;
            pumpCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var reader = new StreamReader(opened.BaseStream, opened.Encoding);
            pump = PumpAsync(reader, pumpCancellation.Token);

            SetState(DeviceState.Connected, portName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not open {PortName}", portName);
            SetState(DeviceState.Faulted, exception.Message);
            await DisconnectAsync();
            throw;
        }
    }

    public override async Task DisconnectAsync()
    {
        if (pumpCancellation is not null)
        {
            await pumpCancellation.CancelAsync();
        }

        // The port is closed before the loop is awaited, and the order matters. SerialPort's
        // BaseStream ignores the cancellation token: a read already in progress stays blocked until
        // a byte happens to arrive, so awaiting the loop first would hang until the instrument sent
        // something — or for ever if it had been unplugged. Disposing the port aborts that read,
        // which is what lets the loop finish.
        port?.Dispose();
        port = null;

        if (pump is not null)
        {
            await pump;
            pump = null;
        }

        pumpCancellation?.Dispose();
        pumpCancellation = null;

        if (State != DeviceState.Faulted)
        {
            SetState(DeviceState.Disconnected);
        }
    }
}
