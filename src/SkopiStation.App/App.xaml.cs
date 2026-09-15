using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkopiStation.App.ViewModels;
using SkopiStation.App.Views;
using SkopiStation.Data;

namespace SkopiStation.App;

/// <summary>
/// Composition root: builds the host, starts it, prepares the database and shows the shell.
/// This is the only code-behind in the application that contains logic, and it is also the only
/// place that assigns a <see cref="FrameworkElement.DataContext"/> — the views never do.
/// </summary>
public partial class App : Application
{
    private readonly IHost host = CreateHostBuilder([]).Build();

    /// <summary>
    /// Builds the application host. The Entity Framework Core tools look for a static
    /// <c>CreateHostBuilder</c> on the startup assembly's entry point type, so
    /// <c>dotnet ef</c> resolves the DbContext from this very container. That is what allows the
    /// connection string to live in appsettings.json only, with no design-time duplicate.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            // The content root is pinned to the output folder so appsettings.json is found whether
            // the process is started from the shell, from the IDE, or by the EF Core tools.
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("SkopiStation")
                    ?? throw new InvalidOperationException(
                        "Connection string 'SkopiStation' is missing from configuration.");

                services.AddSkopiStationData(connectionString);
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<MainWindow>();
            });

    // async void is deliberate and allowed here: this is a WPF event handler. Startup work is
    // awaited rather than blocked on, so no .Result or .Wait() appears anywhere in the codebase.
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        await host.StartAsync();

        var shell = host.Services.GetRequiredService<ShellViewModel>();
        var window = host.Services.GetRequiredService<MainWindow>();
        window.DataContext = shell;
        window.Show();

        await shell.InitializeAsync(CancellationToken.None);
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        await host.StopAsync();
        host.Dispose();
    }
}
