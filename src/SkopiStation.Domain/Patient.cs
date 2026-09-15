namespace SkopiStation.Domain;

public class Patient
{
    public Guid Id { get; set; }

    public required string LastName { get; set; }

    public required string FirstName { get; set; }

    public DateOnly BirthDate { get; set; }

    public required string RecordNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Measurement> Measurements { get; } = new List<Measurement>();
}
