using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SkopiStation.Domain;

namespace SkopiStation.Devices;

/// <summary>A frame accepted by <see cref="MeasurementFrameParser"/>.</summary>
public sealed record MeasurementFrame(MeasurementKind Kind, decimal Value, string Unit, DateTimeOffset TakenAt);

/// <summary>
/// Turns a line of the device protocol into a <see cref="MeasurementFrame"/>.
/// </summary>
/// <remarks>
/// Parsing is deliberately kept away from anything to do with serial ports: it is pure text in,
/// value out, so the whole protocol can be tested without hardware.
/// The expected shape is <c>MEAS|KIND|VALUE|UNIT|ISO8601</c>, for example
/// <c>MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z</c>.
/// </remarks>
public static class MeasurementFrameParser
{
    private const string Prefix = "MEAS";
    private const int FieldCount = 5;

    public static bool TryParse(
        string? line,
        [NotNullWhen(true)] out MeasurementFrame? frame,
        [NotNullWhen(false)] out string? error)
    {
        frame = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "the frame is empty";
            return false;
        }

        var fields = line.Trim().Split('|');

        if (fields.Length != FieldCount)
        {
            error = $"expected {FieldCount} fields separated by '|', found {fields.Length}";
            return false;
        }

        if (!string.Equals(fields[0], Prefix, StringComparison.Ordinal))
        {
            error = $"unknown frame type '{fields[0]}'";
            return false;
        }

        // Enum.TryParse would accept a bare number such as "1", which is not a protocol value.
        if (!Enum.TryParse<MeasurementKind>(fields[1], ignoreCase: false, out var kind)
            || !Enum.IsDefined(kind)
            || char.IsDigit(fields[1][0]))
        {
            error = $"unknown measurement kind '{fields[1]}'";
            return false;
        }

        // Deliberately not NumberStyles.Number: it allows group separators, and the invariant group
        // separator is the comma. A device sending "2,5" would then be read as 25 — a plausible
        // axial length, silently stored. The protocol carries machine-formatted numbers, so a
        // decimal point and an optional sign are all that is accepted.
        const NumberStyles ProtocolNumber = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        if (!decimal.TryParse(fields[2], ProtocolNumber, CultureInfo.InvariantCulture, out var value))
        {
            error = $"'{fields[2]}' is not a number";
            return false;
        }

        // The unit is not merely recorded, it is checked: a pressure reported in kPa means the
        // device is not configured as expected, and silently storing the number would be worse
        // than dropping the frame.
        var canonicalUnit = kind.CanonicalUnit();
        if (!string.Equals(fields[3], canonicalUnit, StringComparison.Ordinal))
        {
            error = $"{kind} is measured in {canonicalUnit}, the frame reports {fields[3]}";
            return false;
        }

        // Physically impossible, not merely out of the clinical reference range: such a value is a
        // corrupt frame or a faulty device. A value outside the reference range is a valid
        // measurement and is accepted here, then flagged by Measurement.IsOutOfRange.
        var plausible = kind.PlausibleRange();
        if (!plausible.Contains(value))
        {
            error = $"{value} {canonicalUnit} is outside the plausible range "
                + $"{plausible.Min}-{plausible.Max} for {kind}";
            return false;
        }

        if (!DateTimeOffset.TryParse(
                fields[4],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var takenAt))
        {
            error = $"'{fields[4]}' is not an ISO 8601 date";
            return false;
        }

        frame = new MeasurementFrame(kind, value, canonicalUnit, takenAt);
        error = null;
        return true;
    }
}
