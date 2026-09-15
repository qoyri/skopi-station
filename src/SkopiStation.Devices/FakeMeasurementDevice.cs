using System.Globalization;
using Microsoft.Extensions.Logging;
using SkopiStation.Domain;

namespace SkopiStation.Devices;

/// <summary>
/// Emits frames from memory, for the tests and for demonstrating the application without an
/// instrument or a virtual port pair.
/// </summary>
/// <remarks>
/// Frames go through <see cref="MeasurementDeviceBase.HandleLine"/> exactly like those read from a
/// serial port, malformed ones included: what the application sees is the same code path, which is
/// what makes this a useful stand-in rather than a shortcut.
/// </remarks>
public sealed class FakeMeasurementDevice(ILogger<FakeMeasurementDevice> logger)
    : MeasurementDeviceBase(logger)
{
    private static readonly string[] DefaultScript =
    [
        Frame(MeasurementKind.IntraocularPressure, 16.4m),
        Frame(MeasurementKind.AxialLength, 23.62m),
        Frame(MeasurementKind.CornealThickness, 545m),
        // Outside the reference range: a valid measurement, recorded and flagged.
        Frame(MeasurementKind.IntraocularPressure, 24.9m),
        // Malformed: logged and ignored, the stream carries on.
        "MEAS|IntraocularPressure|16.4|kPa|2026-09-15T10:22:31Z",
        Frame(MeasurementKind.CornealThickness, 612m),
    ];

    private CancellationTokenSource? loopCancellation;
    private Task? loop;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);

    public IReadOnlyList<string> Script { get; set; } = DefaultScript;

    /// <summary>Pushes a single line through the normal pipeline, for deterministic tests.</summary>
    public void Emit(string line) => HandleLine(line);

    public override Task ConnectAsync(string portName, CancellationToken ct)
    {
        SetState(DeviceState.Connecting, portName);

        loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        loop = RunScriptAsync(loopCancellation.Token);

        SetState(DeviceState.Connected, portName);
        return Task.CompletedTask;
    }

    public override async Task DisconnectAsync()
    {
        if (loopCancellation is not null)
        {
            await loopCancellation.CancelAsync();
        }

        if (loop is not null)
        {
            await loop;
            loop = null;
        }

        loopCancellation?.Dispose();
        loopCancellation = null;

        SetState(DeviceState.Disconnected);
    }

    private static string Frame(MeasurementKind kind, decimal value) => string.Join(
        '|',
        "MEAS",
        kind,
        value.ToString(CultureInfo.InvariantCulture),
        kind.CanonicalUnit(),
        "2026-09-15T10:22:31Z");

    private async Task RunScriptAsync(CancellationToken ct)
    {
        try
        {
            var index = 0;

            while (!ct.IsCancellationRequested && Script.Count > 0)
            {
                await Task.Delay(Interval, ct);
                HandleLine(Script[index % Script.Count]);
                index++;
            }
        }
        catch (OperationCanceledException)
        {
            // Disconnecting cancels the token; that is the normal way out of this loop.
        }
    }
}
