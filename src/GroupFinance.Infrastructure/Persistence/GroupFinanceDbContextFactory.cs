using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GroupFinance.Infrastructure.Persistence;

public sealed class GroupFinanceDbContextFactory : IDesignTimeDbContextFactory<GroupFinanceDbContext>
{
    public GroupFinanceDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "GroupFinance.Api"));
        if (!Directory.Exists(apiPath))
            apiPath = Path.GetFullPath(Path.Combine(basePath, "src", "GroupFinance.Api"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiPath) ? apiPath : basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var options = new DbContextOptionsBuilder<GroupFinanceDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        return new GroupFinanceDbContext(options);
    }
}
