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

    public MeasurementFrame Frame { get; } = frame;

    public MeasurementSource Source { get; } = source;

    public DateTimeOffset TakenAt => Frame.TakenAt;

    public MeasurementKind Kind => Frame.Kind;

    public decimal Value => Frame.Value;

    public string Unit => Frame.Unit;

    public bool IsOutOfRange => !Frame.Kind.ReferenceRange().Contains(Frame.Value);

    public bool IsAttached => AttachedTo is not null;

    public Measurement ToMeasurement(Guid patientId) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = patientId,
        TakenAt = Frame.TakenAt,
        Kind = Frame.Kind,
        Value = Frame.Value,
        Unit = Frame.Unit,
        Source = Source,
    };

    public override string ToString() =>
        $"{Kind} {Value} {Unit} at {TakenAt:t}{(IsAttached ? $" — {AttachedTo}" : string.Empty)}";

    partial void OnAttachedToChanged(string? value) => OnPropertyChanged(nameof(IsAttached));
}
