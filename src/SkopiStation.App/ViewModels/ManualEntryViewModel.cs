using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SkopiStation.Devices;
using SkopiStation.Domain;

namespace SkopiStation.App.ViewModels;

/// <summary>
/// Manual entry of a measurement, for a reading taken without the instrument connected.
/// </summary>
/// <remarks>
/// The value is validated against the same plausibility envelope the frame parser applies, so a
/// typed reading cannot enter the database by a route a received one could not.
/// </remarks>
public sealed partial class ManualEntryViewModel : ObservableValidator
{
    [ObservableProperty]
    private MeasurementKind kind = MeasurementKind.IntraocularPressure;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A value is required.")]
    [CustomValidation(typeof(ManualEntryViewModel), nameof(ValidateValue))]
    private string value = string.Empty;

    public static IReadOnlyList<MeasurementKind> Kinds { get; } = Enum.GetValues<MeasurementKind>();

    public string Unit => Kind.CanonicalUnit();

    public bool CanSubmit => !HasErrors && !string.IsNullOrWhiteSpace(Value);

    public MeasurementFrame ToFrame() => new(
        Kind,
        decimal.Parse(Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
        Kind.CanonicalUnit(),
        DateTimeOffset.Now);

    public void Reset()
    {
        Value = string.Empty;
        ClearErrors();
    }

    public static ValidationResult? ValidateValue(string? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        var entry = (ManualEntryViewModel)context.ObjectInstance;

        if (!decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return new ValidationResult("Enter a number, using a dot as the decimal separator.");
        }

        var plausible = entry.Kind.PlausibleRange();

        return plausible.Contains(parsed)
            ? ValidationResult.Success
            : new ValidationResult(
                $"A {entry.Kind} between {plausible.Min} and {plausible.Max} {entry.Unit} is expected.");
    }

    partial void OnKindChanged(MeasurementKind value)
    {
        OnPropertyChanged(nameof(Unit));

        // The plausible range depends on the kind, so a value that was acceptable for a pressure
        // has to be judged again as a length.
        ValidateProperty(Value, nameof(Value));
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(CanSubmit));
}
