using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkopiStation.Domain;

namespace SkopiStation.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.LastName).HasMaxLength(100);
        builder.Property(p => p.FirstName).HasMaxLength(100);
        builder.Property(p => p.RecordNumber).HasMaxLength(RecordNumberFormat.Length).IsUnicode(false);

        builder.HasIndex(p => p.RecordNumber).IsUnique();

        builder.HasMany(p => p.Measurements)
            .WithOne()
            .HasForeignKey(m => m.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
