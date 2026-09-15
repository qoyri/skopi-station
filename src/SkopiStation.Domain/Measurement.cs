namespace SkopiStation.Domain;

public class Measurement
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    public MeasurementKind Kind { get; set; }

    public decimal Value { get; set; }

    public required string Unit { get; set; }

    public MeasurementSource Source { get; set; }

    public bool IsOutOfRange => !Kind.ReferenceRange().Contains(Value);
}
