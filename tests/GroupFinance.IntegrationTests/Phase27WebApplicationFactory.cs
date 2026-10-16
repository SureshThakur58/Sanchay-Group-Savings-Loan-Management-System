using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GroupFinance.IntegrationTests;

/// <summary>
/// Dedicated Phase 27 test host. The connection string is supplied through
/// GROUPFINANCE_TEST_CONNECTION_STRING and is injected into the application
/// configuration before the test host starts.
/// </summary>
public sealed class Phase27WebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringVariable = "GROUPFINANCE_TEST_CONNECTION_STRING";
    private const string TestJwtIssuer = "GroupFinance.Test";
    private const string TestJwtAudience = "GroupFinance.Test.Client";
    private const string TestJwtSecret = "Phase27-Test-Secret-Key-Must-Be-At-Least-32-Bytes!";

    public const string TestAdminUsername = "phase27admin";
    public const string TestAdminPassword = "Phase27-Test-Admin-Password-2026!";

    // WebApplication.CreateBuilder reads environment variables while the application
    // builder is being created. With .NET 10 minimal hosting, ConfigureWebHost can
    // run after that point, so JWT settings must be present before the base
    // WebApplicationFactory constructor starts the application.
    private static readonly bool TestEnvironmentInitialized = InitializeTestEnvironment();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                $"Set {ConnectionStringVariable} to a disposable SQL Server test database to run Phase 27 integration tests.");
        }

        builder.UseEnvironment("Testing");
        builder.UseSetting("environment", "Testing");
        // Keep these settings on the host as well as in application configuration.
        // The environment variables above are the authoritative early configuration
        // source; these settings protect the deferred host configuration path.
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:SecretKey", TestJwtSecret);
        // WebApplicationFactory on the .NET 10 minimal-hosting path can build the
        // application configuration before ConfigureAppConfiguration is applied.
        // UseSetting makes the connection string available to the host configuration
        // as well, while ConfigureAppConfiguration keeps it in the application config.
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:SecretKey"] = TestJwtSecret,
                ["BootstrapAdmin:Username"] = TestAdminUsername,
                ["BootstrapAdmin:Password"] = TestAdminPassword,
                ["BootstrapAdmin:FullName"] = "Phase 27 Test Administrator"
            });
        });
    }

    private static bool InitializeTestEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        // These names are understood by the default .NET configuration provider
        // before Program.cs executes. This keeps the JWT used by AuthService and the
        // JWT validation parameters in Program.cs on exactly the same values.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("Jwt__SecretKey", TestJwtSecret);
        Environment.SetEnvironmentVariable("BootstrapAdmin__Username", TestAdminUsername);
        Environment.SetEnvironmentVariable("BootstrapAdmin__Password", TestAdminPassword);
        Environment.SetEnvironmentVariable("BootstrapAdmin__FullName", "Phase 27 Test Administrator");
        return true;
    }
}
