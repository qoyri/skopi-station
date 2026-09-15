using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SkopiStation.Data;

/// <summary>
/// Used only by <c>dotnet ef</c>, so migrations can be generated from this project
/// without building and starting the WPF host.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SkopiStationDbContext>
{
    private const string LocalDbConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=SkopiStation;Trusted_Connection=True";

    public SkopiStationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SkopiStationDbContext>()
            .UseSqlServer(LocalDbConnectionString)
            .Options;

        return new SkopiStationDbContext(options);
    }
}
