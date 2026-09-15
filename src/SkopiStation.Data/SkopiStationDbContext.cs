using Microsoft.EntityFrameworkCore;
using SkopiStation.Domain;

namespace SkopiStation.Data;

public class SkopiStationDbContext(DbContextOptions<SkopiStationDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Measurement> Measurements => Set<Measurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SkopiStationDbContext).Assembly);
}
