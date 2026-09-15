using System.Text.RegularExpressions;

namespace SkopiStation.Domain;

/// <summary>Patient record numbers look like <c>SK-004217</c>: a fixed prefix and six digits.</summary>
public static partial class RecordNumberFormat
{
    public const int Length = 9;

    public static string Format(int sequence) => $"SK-{sequence:D6}";

    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    [GeneratedRegex(@"^SK-\d{6}$")]
    private static partial Regex Pattern();
}
