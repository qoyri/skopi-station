using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkopiStation.Domain;

namespace SkopiStation.Data.Configurations;

internal sealed class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.HasKey(m => m.Id);

        // Enums are stored by name: the table stays readable and reordering an enum cannot corrupt existing rows.
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(m => m.Source).HasConversion<string>().HasMaxLength(16).IsUnicode(false);

        builder.Property(m => m.Value).HasPrecision(8, 2);
        builder.Property(m => m.Unit).HasMaxLength(16);

        builder.HasIndex(m => new { m.PatientId, m.TakenAt });

        builder.Ignore(m => m.IsOutOfRange);
    }
}
