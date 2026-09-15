using System.Globalization;
using System.IO.Ports;
using SkopiStation.Domain;

// Emits measurement frames on a serial port, so the application can be exercised without an
// instrument. Under Windows, pair it with com0com to obtain two linked virtual ports: the
// simulator writes on one, the application reads on the other.
//
//   SerialSimulator <port> [interval-ms]
//   SerialSimulator COM3 500

if (args.Length is 0 or > 2 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: SerialSimulator <port> [interval-ms]");
    Console.WriteLine($"Available ports: {string.Join(", ", SerialPort.GetPortNames())}");
    return 1;
}

var portName = args[0];
var interval = args.Length == 2 && int.TryParse(args[1], out var parsed) ? parsed : 1000;

// A fixed seed keeps a session reproducible, which matters when a frame is being investigated.
var random = new Random(20260915);

using var port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One) { NewLine = "\n" };

try
{
    port.Open();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Could not open {portName}: {exception.Message}");
    Console.Error.WriteLine($"Available ports: {string.Join(", ", SerialPort.GetPortNames())}");
    return 1;
}

Console.WriteLine($"Emitting on {portName} every {interval} ms. Ctrl+C to stop.");

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopping.Cancel();
};

try
{
    while (!stopping.IsCancellationRequested)
    {
        var line = NextFrame();
        port.WriteLine(line);
        Console.WriteLine(line);
        await Task.Delay(interval, stopping.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped.");
}

return 0;

string NextFrame()
{
    // One frame in ten is deliberately broken, so the application's handling of malformed input is
    // visible during a demonstration rather than merely asserted in the tests.
    if (random.Next(10) == 0)
    {
        return random.Next(4) switch
        {
            0 => "MEAS|IntraocularPressure|16.4|kPa|2026-09-15T10:22:31Z",
            1 => "MEAS|IntraocularPressure|16.4",
            2 => "MEAS|Refraction|1.25|D|2026-09-15T10:22:31Z",
            _ => "MEAS|IntraocularPressure|-5|mmHg|2026-09-15T10:22:31Z",
        };
    }

    var kind = (MeasurementKind)random.Next(Enum.GetValues<MeasurementKind>().Length);
    var range = kind.ReferenceRange();
    var margin = (range.Max - range.Min) * 0.25m;

    // One reading in five sits outside the reference range: still a valid measurement, which the
    // application records and flags.
    var (min, max) = random.Next(5) == 0
        ? (range.Max, range.Max + margin)
        : (range.Min, range.Max);

    var value = min + ((max - min) * (decimal)random.NextDouble());
    var decimals = kind switch
    {
        MeasurementKind.IntraocularPressure => 1,
        MeasurementKind.AxialLength => 2,
        _ => 0,
    };

    return string.Join(
        '|',
        "MEAS",
        kind,
        Math.Round(value, decimals).ToString(CultureInfo.InvariantCulture),
        kind.CanonicalUnit(),
        DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
}
