using FluentAssertions;
using SkopiStation.Devices;
using SkopiStation.Domain;

namespace SkopiStation.Tests.Devices;

public class MeasurementFrameParserTests
{
    [Fact]
    public void A_well_formed_frame_is_accepted()
    {
        var parsed = MeasurementFrameParser.TryParse(
            "MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z",
            out var frame,
            out var error);

        parsed.Should().BeTrue();
        error.Should().BeNull();
        frame!.Kind.Should().Be(MeasurementKind.IntraocularPressure);
        frame.Value.Should().Be(16.4m);
        frame.Unit.Should().Be("mmHg");
        frame.TakenAt.Should().Be(new DateTimeOffset(2026, 9, 15, 10, 22, 31, TimeSpan.Zero));
    }

    [Fact]
    public void Surrounding_whitespace_and_a_trailing_newline_are_tolerated()
    {
        var parsed = MeasurementFrameParser.TryParse(
            "  MEAS|AxialLength|23.62|mm|2026-09-15T10:22:31Z\r\n",
            out var frame,
            out _);

        parsed.Should().BeTrue();
        frame!.Value.Should().Be(23.62m);
    }

    [Fact]
    public void A_decimal_value_is_read_with_the_invariant_culture()
    {
        MeasurementFrameParser.TryParse(
            "MEAS|CornealThickness|545.5|µm|2026-09-15T10:22:31Z",
            out var frame,
            out _).Should().BeTrue();

        frame!.Value.Should().Be(545.5m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_frame_is_rejected(string? line)
    {
        MeasurementFrameParser.TryParse(line, out _, out var error).Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Theory]
    [InlineData("MEAS|IntraocularPressure|16.4|mmHg", "found 4")]
    [InlineData("MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z|extra", "found 6")]
    [InlineData("MEAS", "found 1")]
    public void A_frame_with_the_wrong_number_of_fields_is_rejected(string line, string expected)
    {
        MeasurementFrameParser.TryParse(line, out _, out var error).Should().BeFalse();
        error.Should().Contain(expected);
    }

    [Fact]
    public void An_unknown_frame_type_is_rejected()
    {
        MeasurementFrameParser.TryParse(
            "PING|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("unknown frame type");
    }

    [Theory]
    [InlineData("Refraction")]
    [InlineData("intraocularpressure")]
    [InlineData("")]
    public void An_unknown_measurement_kind_is_rejected(string kind)
    {
        MeasurementFrameParser.TryParse(
            $"MEAS|{kind}|16.4|mmHg|2026-09-15T10:22:31Z",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("unknown measurement kind");
    }

    /// <summary>The protocol carries names, not ordinals; accepting "0" would let a corrupt frame
    /// silently become a pressure reading.</summary>
    [Fact]
    public void A_numeric_measurement_kind_is_rejected()
    {
        MeasurementFrameParser.TryParse(
            "MEAS|0|16.4|mmHg|2026-09-15T10:22:31Z",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("unknown measurement kind");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("16,4")]
    [InlineData("1 234")]
    public void A_value_that_is_not_a_number_is_rejected(string value)
    {
        MeasurementFrameParser.TryParse(
            $"MEAS|IntraocularPressure|{value}|mmHg|2026-09-15T10:22:31Z",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("not a number");
    }

    /// <summary>
    /// A comma must never be read as a group separator. "2,5" parsed with NumberStyles.Number
    /// yields 25, which is a plausible axial length inside the reference range: the frame would be
    /// accepted and a wrong value stored without anything to show for it.
    /// </summary>
    [Fact]
    public void A_comma_is_not_read_as_a_group_separator()
    {
        MeasurementFrameParser.TryParse(
            "MEAS|AxialLength|2,5|mm|2026-09-15T10:22:31Z",
            out var frame,
            out var error).Should().BeFalse();

        frame.Should().BeNull();
        error.Should().Contain("not a number");
    }

    [Theory]
    [InlineData(MeasurementKind.IntraocularPressure, "kPa")]
    [InlineData(MeasurementKind.AxialLength, "cm")]
    [InlineData(MeasurementKind.CornealThickness, "mm")]
    [InlineData(MeasurementKind.IntraocularPressure, "MMHG")]
    public void A_unit_that_is_not_the_canonical_one_is_rejected(MeasurementKind kind, string unit)
    {
        MeasurementFrameParser.TryParse(
            $"MEAS|{kind}|20|{unit}|2026-09-15T10:22:31Z",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain(kind.CanonicalUnit()).And.Contain(unit);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-13-45T99:99:99Z")]
    [InlineData("")]
    public void An_invalid_date_is_rejected(string date)
    {
        MeasurementFrameParser.TryParse(
            $"MEAS|IntraocularPressure|16.4|mmHg|{date}",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("ISO 8601");
    }

    /// <summary>
    /// The distinction the whole protocol turns on: a physically impossible value is a broken
    /// frame and is dropped, whereas a value merely outside the clinical reference range is a
    /// genuine measurement that must reach the database and be flagged there.
    /// </summary>
    [Theory]
    [InlineData(MeasurementKind.IntraocularPressure, "-5")]
    [InlineData(MeasurementKind.IntraocularPressure, "0")]
    [InlineData(MeasurementKind.IntraocularPressure, "500")]
    [InlineData(MeasurementKind.AxialLength, "0.5")]
    [InlineData(MeasurementKind.AxialLength, "120")]
    [InlineData(MeasurementKind.CornealThickness, "-1")]
    [InlineData(MeasurementKind.CornealThickness, "5000")]
    public void A_physically_impossible_value_is_rejected(MeasurementKind kind, string value)
    {
        MeasurementFrameParser.TryParse(
            $"MEAS|{kind}|{value}|{kind.CanonicalUnit()}|2026-09-15T10:22:31Z",
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("plausible range");
    }

    [Theory]
    [InlineData(MeasurementKind.IntraocularPressure, "24.9")]
    [InlineData(MeasurementKind.IntraocularPressure, "8.0")]
    [InlineData(MeasurementKind.AxialLength, "26.5")]
    [InlineData(MeasurementKind.CornealThickness, "480")]
    [InlineData(MeasurementKind.CornealThickness, "650")]
    public void A_value_outside_the_reference_range_is_accepted_and_flagged(
        MeasurementKind kind,
        string value)
    {
        var parsed = MeasurementFrameParser.TryParse(
            $"MEAS|{kind}|{value}|{kind.CanonicalUnit()}|2026-09-15T10:22:31Z",
            out var frame,
            out _);

        parsed.Should().BeTrue();

        var measurement = new Measurement
        {
            Kind = frame!.Kind,
            Value = frame.Value,
            Unit = frame.Unit,
        };

        measurement.IsOutOfRange.Should().BeTrue();
    }

    [Fact]
    public void A_value_inside_the_reference_range_is_not_flagged()
    {
        MeasurementFrameParser.TryParse(
            "MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z",
            out var frame,
            out _).Should().BeTrue();

        new Measurement { Kind = frame!.Kind, Value = frame.Value, Unit = frame.Unit }
            .IsOutOfRange.Should().BeFalse();
    }

    [Theory]
    [InlineData(MeasurementKind.IntraocularPressure)]
    [InlineData(MeasurementKind.AxialLength)]
    [InlineData(MeasurementKind.CornealThickness)]
    public void The_reference_range_sits_inside_the_plausible_range(MeasurementKind kind)
    {
        var reference = kind.ReferenceRange();
        var plausible = kind.PlausibleRange();

        plausible.Contains(reference.Min).Should().BeTrue();
        plausible.Contains(reference.Max).Should().BeTrue();
    }
}
