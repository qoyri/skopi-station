using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkopiStation.Data;

namespace SkopiStation.App.ViewModels;

/// <summary>
/// Shell of the application. At this stage it only reports the state of the database, which is the
/// acceptance criterion for the first milestone; the patient and acquisition screens replace this
/// content in the following milestones.
/// </summary>
public sealed partial class ShellViewModel(
    DatabaseInitializer databaseInitializer,
    IDbContextFactory<SkopiStationDbContext> contextFactory,
    ILogger<ShellViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    private string status = "Starting…";

    [ObservableProperty]
    private int patientCount;

    [ObservableProperty]
    private int measurementCount;

    public async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            Status = "Applying migrations and seeding…";
            await databaseInitializer.InitializeAsync(ct);

            // A context per operation, created from the factory and disposed straight away: no
            // long-lived context is ever held by a ViewModel.
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            PatientCount = await context.Patients.CountAsync(ct);
            MeasurementCount = await context.Measurements.CountAsync(ct);

            Status = "Database ready.";
        }
        catch (Exception exception)
        {
            Status = $"Database unavailable: {exception.Message}";
            logger.LogError(exception, "Database initialisation failed");
        }
    }
}
