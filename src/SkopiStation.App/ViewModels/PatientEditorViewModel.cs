using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkopiStation.Data;
using SkopiStation.Domain;

namespace SkopiStation.App.ViewModels;

/// <summary>
/// Identity form of the selected patient.
/// </summary>
/// <remarks>
/// Validation goes through <see cref="ObservableValidator"/>, which implements
/// <see cref="System.ComponentModel.INotifyDataErrorInfo"/> — not the obsolete
/// <c>IDataErrorInfo</c>. The difference matters here: errors are reported per property and
/// asynchronously through <c>ErrorsChanged</c>, so a field can be invalid without the binding
/// having to round-trip a string, and the Save command can simply observe <c>HasErrors</c>.
/// </remarks>
public sealed partial class PatientEditorViewModel(IPatientRepository repository) : ObservableValidator
{
    /// <summary>The oldest birth date the form accepts; beyond this a date is a typing mistake.</summary>
    private const int MaximumAgeInYears = 120;

    private PatientListItemViewModel? patient;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [Required(ErrorMessage = "The record number is required.")]
    [CustomValidation(typeof(PatientEditorViewModel), nameof(ValidateRecordNumber))]
    private string recordNumber = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [Required(ErrorMessage = "The last name is required.")]
    [MaxLength(100, ErrorMessage = "The last name cannot exceed 100 characters.")]
    private string lastName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [Required(ErrorMessage = "The first name is required.")]
    [MaxLength(100, ErrorMessage = "The first name cannot exceed 100 characters.")]
    private string firstName = string.Empty;

    // DateTime? rather than DateOnly: it is what DatePicker binds to. The conversion back to the
    // domain's DateOnly happens on save.
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [Required(ErrorMessage = "The birth date is required.")]
    [CustomValidation(typeof(PatientEditorViewModel), nameof(ValidateBirthDate))]
    private DateTime? birthDate;

    [ObservableProperty]
    private string? statusMessage;

    /// <summary>
    /// Tells whether a record number is already used by another patient. The list owns the loaded
    /// patients, so the check is immediate and the user sees the conflict while typing rather than
    /// as a unique index violation on save.
    /// </summary>
    public Func<string, Guid, bool> IsRecordNumberTaken { get; set; } = (_, _) => false;

    public bool IsLoaded => patient is not null;

    public void Load(PatientListItemViewModel? selected)
    {
        patient = selected;
        StatusMessage = null;

        if (selected is null)
        {
            ClearErrors();
        }
        else
        {
            RecordNumber = selected.RecordNumber;
            LastName = selected.LastName;
            FirstName = selected.FirstName;
            BirthDate = selected.BirthDate.ToDateTime(TimeOnly.MinValue);
            ValidateAllProperties();
        }

        OnPropertyChanged(nameof(IsLoaded));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (patient is null)
        {
            return;
        }

        var identity = new PatientIdentity(
            patient.Id,
            RecordNumber.Trim(),
            LastName.Trim(),
            FirstName.Trim(),
            DateOnly.FromDateTime(BirthDate!.Value));

        try
        {
            await repository.UpdateIdentityAsync(identity, ct);
            patient.Apply(identity);
            StatusMessage = "Saved.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not save: {exception.Message}";
        }
    }

    private bool CanSave() => IsLoaded && !HasErrors;

    public static ValidationResult? ValidateRecordNumber(string? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        var editor = (PatientEditorViewModel)context.ObjectInstance;

        if (!RecordNumberFormat.IsValid(value.Trim()))
        {
            return new ValidationResult("The record number must look like SK-004217.");
        }

        var currentId = editor.patient?.Id ?? Guid.Empty;

        return editor.IsRecordNumberTaken(value.Trim(), currentId)
            ? new ValidationResult("This record number belongs to another patient.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateBirthDate(DateTime? value, ValidationContext _)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var birthDate = DateOnly.FromDateTime(value.Value);
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (birthDate > today)
        {
            return new ValidationResult("The birth date cannot be in the future.");
        }

        return birthDate < today.AddYears(-MaximumAgeInYears)
            ? new ValidationResult($"The birth date cannot be more than {MaximumAgeInYears} years ago.")
            : ValidationResult.Success;
    }
}
