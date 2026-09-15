namespace SkopiStation.Domain;

public readonly record struct ReferenceRange(decimal Min, decimal Max)
{
    public bool Contains(decimal value) => value >= Min && value <= Max;
}
