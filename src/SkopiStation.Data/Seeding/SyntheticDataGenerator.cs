using Bogus;
using SkopiStation.Domain;

namespace SkopiStation.Data.Seeding;

/// <summary>Generates the synthetic patients and measurements used to seed an empty database.</summary>
public static class SyntheticDataGenerator
{
    public const int PatientCount = 50;

    private const int Seed = 20260915;

    // Bogus computes relative dates from DateTime.Now; a fixed reference date is what makes the dataset
    // identical from one run, and one machine, to the next. The seed alone is not enough.
    private static readonly DateTimeOffset ReferenceDate = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<Patient> Generate()
    {
        var faker = new Faker("fr") { Random = new Randomizer(Seed) };

        return Enumerable.Range(1, PatientCount)
            .Select(sequence => CreatePatient(faker, sequence))
            .ToList();
    }

    private static Patient CreatePatient(Faker faker, int sequence)
    {
        var patient = new Patient
        {
            Id = faker.Random.Guid(),
            LastName = faker.Name.LastName(),
            FirstName = faker.Name.FirstName(),
            BirthDate = faker.Date.BetweenDateOnly(new DateOnly(1935, 1, 1), new DateOnly(2010, 12, 31)),
            RecordNumber = RecordNumberFormat.Format(1000 + sequence),
            CreatedAt = faker.Date.BetweenOffset(ReferenceDate.AddYears(-3), ReferenceDate.AddMonths(-1)),
        };

        var measurementCount = faker.Random.Int(2, 10);
        for (var i = 0; i < measurementCount; i++)
        {
            patient.Measurements.Add(CreateMeasurement(faker, patient));
        }

        return patient;
    }

    private static Measurement CreateMeasurement(Faker faker, Patient patient)
    {
        var kind = faker.Random.Enum<MeasurementKind>();

        return new Measurement
        {
            Id = faker.Random.Guid(),
            PatientId = patient.Id,
            TakenAt = faker.Date.BetweenOffset(patient.CreatedAt, ReferenceDate),
            Kind = kind,
            Value = CreateValue(faker, kind),
            Unit = kind.CanonicalUnit(),
            Source = faker.Random.Bool(0.7f) ? MeasurementSource.Device : MeasurementSource.Manual,
        };
    }

    private static decimal CreateValue(Faker faker, MeasurementKind kind)
    {
        var range = kind.ReferenceRange();
        var margin = (range.Max - range.Min) * 0.3m;

        // About one reading in six lands outside the reference range, so out-of-range values show up in the UI.
        var (min, max) = faker.Random.Bool(0.85f)
            ? (range.Min, range.Max)
            : faker.PickRandom((range.Min - margin, range.Min), (range.Max, range.Max + margin));

        var decimals = kind switch
        {
            MeasurementKind.IntraocularPressure => 1,
            MeasurementKind.AxialLength => 2,
            _ => 0,
        };

        return Math.Round(faker.Random.Decimal(min, max), decimals);
    }
}
