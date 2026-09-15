using CommunityToolkit.Mvvm.ComponentModel;
using SkopiStation.Data;

namespace SkopiStation.App.ViewModels;

/// <summary>One row of the patient grid.</summary>
public sealed partial class PatientListItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string recordNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string lastName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string firstName;

    [ObservableProperty]
    private DateOnly birthDate;

    [ObservableProperty]
    private int measurementCount;

    public PatientListItemViewModel(PatientSummary summary)
    {
        Id = summary.Id;
        recordNumber = summary.RecordNumber;
        lastName = summary.LastName;
        firstName = summary.FirstName;
        birthDate = summary.BirthDate;
        measurementCount = summary.MeasurementCount;
    }

    public Guid Id { get; }

    public string FullName => $"{LastName.ToUpperInvariant()} {FirstName}";

    /// <summary>Matches the search box against the last name, the first name and the record number.</summary>
    public bool Matches(string term) =>
        string.IsNullOrWhiteSpace(term)
        || LastName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || FirstName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || RecordNumber.Contains(term, StringComparison.CurrentCultureIgnoreCase);

    public void Apply(PatientIdentity identity)
    {
        RecordNumber = identity.RecordNumber;
        LastName = identity.LastName;
        FirstName = identity.FirstName;
        BirthDate = identity.BirthDate;
    }
}
