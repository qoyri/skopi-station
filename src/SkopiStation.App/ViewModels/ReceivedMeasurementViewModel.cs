using CommunityToolkit.Mvvm.ComponentModel;
using SkopiStation.Devices;
using SkopiStation.Domain;

namespace SkopiStation.App.ViewModels;

/// <summary>One reading in the live feed, before it is attached to a patient.</summary>
public sealed partial class ReceivedMeasurementViewModel(MeasurementFrame frame, MeasurementSource source)
    : ObservableObject
{
    [ObservableProperty]
    private string? attachedTo;

    public MeasurementSource Source { get; } = source;

    public DateTimeOffset TakenAt => frame.TakenAt;

    public MeasurementKind Kind => frame.Kind;

    public decimal Value => frame.Value;

    public string Unit => frame.Unit;

    public bool IsOutOfRange => !frame.Kind.ReferenceRange().Contains(frame.Value);

    public bool IsAttached => AttachedTo is not null;

    public Measurement ToMeasurement(Guid patientId) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = patientId,
        TakenAt = frame.TakenAt,
        Kind = frame.Kind,
        Value = frame.Value,
        Unit = frame.Unit,
        Source = Source,
    };

    public override string ToString() =>
        $"{Kind} {Value} {Unit} at {TakenAt:t}{(IsAttached ? $" — {AttachedTo}" : string.Empty)}";

    partial void OnAttachedToChanged(string? value) => OnPropertyChanged(nameof(IsAttached));
}
