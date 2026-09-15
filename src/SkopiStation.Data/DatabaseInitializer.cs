using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkopiStation.Data.Seeding;

namespace SkopiStation.Data;

public sealed class DatabaseInitializer(
    IDbContextFactory<SkopiStationDbContext> contextFactory,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        await context.Database.MigrateAsync(ct);

        if (await context.Patients.AnyAsync(ct))
        {
            return;
        }

        var patients = SyntheticDataGenerator.Generate();
        context.Patients.AddRange(patients);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {PatientCount} synthetic patients and {MeasurementCount} measurements",
            patients.Count,
            patients.Sum(p => p.Measurements.Count));
    }
}
