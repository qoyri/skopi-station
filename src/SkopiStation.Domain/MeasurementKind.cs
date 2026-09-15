namespace SkopiStation.Domain;

public enum MeasurementKind
{
    IntraocularPressure,
    AxialLength,
    CornealThickness,
}

public static class MeasurementKindExtensions
{
    public static string CanonicalUnit(this MeasurementKind kind) => kind switch
    {
        MeasurementKind.IntraocularPressure => "mmHg",
        MeasurementKind.AxialLength => "mm",
        MeasurementKind.CornealThickness => "µm",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // Indicative adult ranges, sufficient for a demo on synthetic data; not clinical guidance.
    public static ReferenceRange ReferenceRange(this MeasurementKind kind) => kind switch
    {
        MeasurementKind.IntraocularPressure => new ReferenceRange(10m, 21m),
        MeasurementKind.AxialLength => new ReferenceRange(22m, 25m),
        MeasurementKind.CornealThickness => new ReferenceRange(500m, 600m),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
