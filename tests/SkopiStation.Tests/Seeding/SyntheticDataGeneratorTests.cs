using FluentAssertions;
using SkopiStation.Data.Seeding;
using SkopiStation.Domain;

namespace SkopiStation.Tests.Seeding;

public class SyntheticDataGeneratorTests
{
    /// <summary>
    /// The seeded dataset has to be identical from one run, and one machine, to the next: screenshots
    /// in the README and any future assertion on seeded rows depend on it. Bogus derives relative
    /// dates from <see cref="DateTime.Now"/>, so a fixed seed alone would not be enough — the
    /// generator also pins a reference date. Generating twice and comparing is what proves both.
    /// </summary>
    [Fact]
    public void Generate_is_reproducible_across_calls()
    {
        var first = SyntheticDataGenerator.Generate();
        var second = SyntheticDataGenerator.Generate();

        Describe(second).Should().Equal(Describe(first));
    }

    [Fact]
    public void Generate_produces_the_expected_volume()
    {
        var patients = SyntheticDataGenerator.Generate();

        patients.Should().HaveCount(SyntheticDataGenerator.PatientCount);
        patients.Sum(patient => patient.Measurements.Count).Should().BeInRange(100, 500);
    }

    [Fact]
    public void Generated_patients_have_unique_well_formed_record_numbers()
    {
        var patients = SyntheticDataGenerator.Generate();

        patients.Should().OnlyContain(patient => RecordNumberFormat.IsValid(patient.RecordNumber));
        patients.Select(patient => patient.RecordNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generated_measurements_use_the_canonical_unit_of_their_kind()
    {
        var measurements = SyntheticDataGenerator.Generate().SelectMany(patient => patient.Measurements);

        measurements.Should().OnlyContain(measurement => measurement.Unit == measurement.Kind.CanonicalUnit());
    }

    private static IReadOnlyList<string> Describe(IEnumerable<Patient> patients) =>
    [
        .. patients.Select(patient => string.Join(
            '|',
            patient.Id,
            patient.LastName,
            patient.FirstName,
            patient.BirthDate,
            patient.RecordNumber,
            patient.CreatedAt,
            string.Join(
                ';',
                patient.Measurements.Select(measurement => string.Join(
                    ',',
                    measurement.Id,
                    measurement.TakenAt,
                    measurement.Kind,
                    measurement.Value,
                    measurement.Unit,
                    measurement.Source))))),
    ];
}
