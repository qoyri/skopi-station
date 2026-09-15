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

    /// <summary>
    /// The envelope of values a working instrument can physically report, much wider than the
    /// reference range.
    /// </summary>
    /// <remarks>
    /// The two ranges answer different questions and must not be confused. A value outside
    /// <see cref="ReferenceRange"/> is a clinical signal: the measurement is valid, it is recorded,
    /// and it is flagged. A value outside this envelope is not a measurement at all but a
    /// corrupt frame or a faulty device, and is rejected before it reaches the database.
    /// These bounds are indicative and chosen for the demonstration.
    /// </remarks>
    public static ReferenceRange PlausibleRange(this MeasurementKind kind) => kind switch
    {
        MeasurementKind.IntraocularPressure => new ReferenceRange(1m, 80m),
        MeasurementKind.AxialLength => new ReferenceRange(14m, 40m),
        MeasurementKind.CornealThickness => new ReferenceRange(200m, 1200m),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
