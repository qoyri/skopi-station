using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkopiStation.Data;
using SkopiStation.Domain;

namespace SkopiStation.App.ViewModels;

/// <summary>Patient list, its search box and the detail panel of the selected row.</summary>
public sealed partial class PatientListViewModel : ObservableObject
{
    private readonly IPatientRepository repository;
    private readonly ObservableCollection<PatientListItemViewModel> patients = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PatientListItemViewModel? selectedPatient;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    public PatientListViewModel(IPatientRepository repository, PatientEditorViewModel editor)
    {
        this.repository = repository;
        Editor = editor;
        Editor.IsRecordNumberTaken = IsRecordNumberTaken;

        // The view is built once over the backing collection and never replaced. Filtering and
        // sorting are then handled by the view itself: typing in the search box calls Refresh()
        // instead of rebuilding a collection, and clicking a column header only appends a
        // SortDescription. The DataGrid binds to this view, not to the collection.
        PatientsView = CollectionViewSource.GetDefaultView(patients);
        PatientsView.Filter = item => item is PatientListItemViewModel patient && patient.Matches(SearchText);
        PatientsView.SortDescriptions.Add(
            new SortDescription(nameof(PatientListItemViewModel.LastName), ListSortDirection.Ascending));
    }

    public ICollectionView PatientsView { get; }

    public PatientEditorViewModel Editor { get; }

    public ObservableCollection<Measurement> Measurements { get; } = [];

    public async Task LoadAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            var summaries = await repository.GetSummariesAsync(ct);

            patients.Clear();
            foreach (var summary in summaries)
            {
                patients.Add(new PatientListItemViewModel(summary));
            }

            StatusMessage = $"{patients.Count} patients loaded.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not load patients: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Called when the acquisition screen files a measurement: keeps the row count honest and
    /// refreshes the history if that patient happens to be the one on screen.
    /// </summary>
    public async Task MeasurementAddedAsync(Guid patientId, CancellationToken ct)
    {
        var patient = patients.FirstOrDefault(candidate => candidate.Id == patientId);

        if (patient is null)
        {
            return;
        }

        patient.MeasurementCount++;

        if (SelectedPatient == patient)
        {
            await LoadMeasurementsAsync(patient, ct);
        }
    }

    partial void OnSearchTextChanged(string value) => PatientsView.Refresh();

    partial void OnSelectedPatientChanged(PatientListItemViewModel? value)
    {
        Editor.Load(value);

        // Not awaited on purpose: this runs from a property setter, and turning the setter into an
        // async void would be exactly the pattern the project bans. The command owns the task and
        // LoadMeasurementsAsync handles its own failures, so nothing goes unobserved.
        LoadMeasurementsCommand.Execute(value);
    }

    [RelayCommand]
    private async Task LoadMeasurementsAsync(PatientListItemViewModel? patient, CancellationToken ct)
    {
        Measurements.Clear();

        if (patient is null)
        {
            return;
        }

        try
        {
            var measurements = await repository.GetMeasurementsAsync(patient.Id, ct);

            foreach (var measurement in measurements)
            {
                Measurements.Add(measurement);
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not load measurements: {exception.Message}";
        }
    }

    private bool IsRecordNumberTaken(string recordNumber, Guid excludedPatientId) =>
        patients.Any(patient =>
            patient.Id != excludedPatientId
            && string.Equals(patient.RecordNumber, recordNumber, StringComparison.OrdinalIgnoreCase));
}
