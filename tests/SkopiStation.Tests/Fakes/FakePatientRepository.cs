using SkopiStation.Data;
using SkopiStation.Domain;

namespace SkopiStation.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="IPatientRepository"/>, so ViewModels can be tested
/// without a database.</summary>
internal sealed class FakePatientRepository : IPatientRepository
{
    private readonly List<PatientSummary> summaries = [];
    private readonly Dictionary<Guid, List<Measurement>> measurements = [];

    public List<PatientIdentity> SavedIdentities { get; } = [];

    public PatientSummary Add(string recordNumber, string lastName, string firstName, DateOnly birthDate)
    {
        var summary = new PatientSummary(Guid.NewGuid(), recordNumber, lastName, firstName, birthDate, 0);
        summaries.Add(summary);
        return summary;
    }

    public void AddMeasurement(Guid patientId, MeasurementKind kind, decimal value)
    {
        if (!measurements.TryGetValue(patientId, out var list))
        {
            list = [];
            measurements[patientId] = list;
        }

        list.Add(new Measurement
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TakenAt = DateTimeOffset.UnixEpoch.AddDays(list.Count),
            Kind = kind,
            Value = value,
            Unit = kind.CanonicalUnit(),
            Source = MeasurementSource.Device,
        });
    }

    public Task<IReadOnlyList<PatientSummary>> GetSummariesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PatientSummary>>(summaries);

    public Task<IReadOnlyList<Measurement>> GetMeasurementsAsync(Guid patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Measurement>>(
            measurements.TryGetValue(patientId, out var list) ? list : []);

    public Task UpdateIdentityAsync(PatientIdentity identity, CancellationToken ct)
    {
        SavedIdentities.Add(identity);
        return Task.CompletedTask;
    }
}
