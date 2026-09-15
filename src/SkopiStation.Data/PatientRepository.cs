using Microsoft.EntityFrameworkCore;
using SkopiStation.Domain;

namespace SkopiStation.Data;

/// <summary>Row of the patient list: the identity plus the number of measurements on file.</summary>
public sealed record PatientSummary(
    Guid Id,
    string RecordNumber,
    string LastName,
    string FirstName,
    DateOnly BirthDate,
    int MeasurementCount);

/// <summary>The editable part of a patient, as submitted by the identity form.</summary>
public sealed record PatientIdentity(
    Guid Id,
    string RecordNumber,
    string LastName,
    string FirstName,
    DateOnly BirthDate);

public interface IPatientRepository
{
    Task<IReadOnlyList<PatientSummary>> GetSummariesAsync(CancellationToken ct);

    Task<IReadOnlyList<Measurement>> GetMeasurementsAsync(Guid patientId, CancellationToken ct);

    Task UpdateIdentityAsync(PatientIdentity identity, CancellationToken ct);
}

/// <summary>
/// Every method opens its own context from the factory and disposes it before returning. Nothing
/// here outlives a single operation, which is what keeps change tracking from accumulating and
/// makes the repository safe to call from any thread.
/// </summary>
public sealed class PatientRepository(IDbContextFactory<SkopiStationDbContext> contextFactory) : IPatientRepository
{
    public async Task<IReadOnlyList<PatientSummary>> GetSummariesAsync(CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        return await context.Patients
            .AsNoTracking()
            .Select(patient => new PatientSummary(
                patient.Id,
                patient.RecordNumber,
                patient.LastName,
                patient.FirstName,
                patient.BirthDate,
                patient.Measurements.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Measurement>> GetMeasurementsAsync(Guid patientId, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        return await context.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.PatientId == patientId)
            .OrderByDescending(measurement => measurement.TakenAt)
            .ToListAsync(ct);
    }

    public async Task UpdateIdentityAsync(PatientIdentity identity, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var patient = await context.Patients.FirstOrDefaultAsync(candidate => candidate.Id == identity.Id, ct)
            ?? throw new InvalidOperationException($"Patient {identity.Id} no longer exists.");

        patient.RecordNumber = identity.RecordNumber;
        patient.LastName = identity.LastName;
        patient.FirstName = identity.FirstName;
        patient.BirthDate = identity.BirthDate;

        await context.SaveChangesAsync(ct);
    }
}
