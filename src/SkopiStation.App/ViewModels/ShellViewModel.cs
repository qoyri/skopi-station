using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SkopiStation.Data;

namespace SkopiStation.App.ViewModels;

/// <summary>
/// Shell of the application: prepares the database, then hands over to the patient screen.
/// </summary>
public sealed partial class ShellViewModel(
    DatabaseInitializer databaseInitializer,
    PatientListViewModel patients,
    ILogger<ShellViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    private string status = "Starting…";

    public PatientListViewModel Patients { get; } = patients;

    public async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            Status = "Applying migrations and seeding…";
            await databaseInitializer.InitializeAsync(ct);

            await Patients.LoadAsync(ct);
            Status = "Database ready.";
        }
        catch (Exception exception)
        {
            Status = $"Database unavailable: {exception.Message}";
            logger.LogError(exception, "Database initialisation failed");
        }
    }
}
