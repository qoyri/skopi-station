using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SkopiStation.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddSkopiStationData(this IServiceCollection services, string connectionString)
    {
        // A factory rather than a scoped DbContext: a desktop app has no request scope, and a context kept
        // alive by a ViewModel would accumulate tracked entities, serve stale data and be used from several
        // threads. Every operation creates its own short-lived context instead.
        services.AddDbContextFactory<SkopiStationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddTransient<DatabaseInitializer>();
        return services;
    }
}
