using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace GroupFinance.Infrastructure.Persistence;

/// <summary>
/// Initializes the database according to the hosting environment.
/// Development/Testing may use EnsureCreated for disposable databases.
/// Staging/Production should use versioned EF Core migrations executed by deployment tooling.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<GroupFinanceDbContext>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var configuration = services.GetRequiredService<IConfiguration>();

        var autoMigrate = configuration.GetValue("Database:AutoMigrate", false);

        try
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
                logger.LogInformation("GroupFinance development/test database initialization completed successfully.");
                return;
            }

            if (autoMigrate)
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("GroupFinance EF Core migrations applied successfully.");
                return;
            }

            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Production database is not reachable. Apply the approved EF Core migrations before starting the application.");
            }

            logger.LogInformation("GroupFinance production database connectivity verified. Automatic migration is disabled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "GroupFinance database initialization failed. Verify the configured SQL Server connection and database deployment state.");
            throw;
        }
    }
}
