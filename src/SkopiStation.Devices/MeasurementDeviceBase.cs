using Microsoft.Extensions.Logging;

namespace SkopiStation.Devices;

/// <summary>
/// Shared behaviour of every device: the reading loop, the handling of a malformed frame and the
/// publication of state changes.
/// </summary>
/// <remarks>
/// The rule that a malformed frame is logged and ignored lives here rather than in each
/// implementation, so there is a single place where it can be got wrong. A device that stops
/// feeding the application because one frame was corrupt would be worse than useless.
/// </remarks>
public abstract class MeasurementDeviceBase(ILogger logger) : IMeasurementDevice, IAsyncDisposable
{
    public event EventHandler<MeasurementReceivedEventArgs>? MeasurementReceived;

    public event EventHandler<DeviceStateChangedEventArgs>? StateChanged;

    private string? lastMessage;

    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    public abstract Task ConnectAsync(string portName, CancellationToken ct);

    public abstract Task DisconnectAsync();

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Publishes a state change, ignoring repetitions. Connecting starts by disconnecting and
    /// disposing ends by disconnecting, so without this guard a single session would report
    /// Disconnected several times and a UI bound to the event would flicker.
    /// </summary>
    protected void SetState(DeviceState state, string? message = null)
    {
        if (State == state && lastMessage == message)
        {
            return;
        }

        State = state;
        lastMessage = message;
        StateChanged?.Invoke(this, new DeviceStateChangedEventArgs(state, message));
    }

    /// <summary>Parses one line and publishes it, or logs why it was dropped.</summary>
    protected void HandleLine(string? line)
    {
        if (MeasurementFrameParser.TryParse(line, out var frame, out var error))
        {
            MeasurementReceived?.Invoke(this, new MeasurementReceivedEventArgs(frame));
            return;
        }

        logger.LogWarning("Discarded frame '{Frame}': {Reason}", line, error);
    }

    /// <summary>Reads lines until the reader runs out or the token is cancelled.</summary>
    protected async Task PumpAsync(TextReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);

                if (line is null)
                {
                    break;
                }

                HandleLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Disconnecting cancels the token; that is the normal way out of this loop.
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Disconnecting also closes the underlying port, which aborts the read in progress with
            // an IO or disposal error. Once cancellation has been requested that is expected, not a
            // fault: reporting it would leave the device stuck in Faulted after a normal stop.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The device reading loop stopped");
            SetState(DeviceState.Faulted, exception.Message);
        }
    }
}
