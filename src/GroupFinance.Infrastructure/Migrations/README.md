# EF Core migrations

The project uses EF Core 10.0 with SQL Server.

For environments where migrations are generated and applied through the .NET CLI:

```powershell
dotnet ef migrations add InitialDatabaseFoundation -Project GroupFinance.Infrastructure -StartupProject GroupFinance.Api -OutputDir Migrations

dotnet ef database update -Project GroupFinance.Infrastructure -StartupProject GroupFinance.Api
```

For the current development workflow, the API initializes a new database with `EnsureCreated` and idempotently adds the Phase 3 authentication schema to an existing Phase 2 database. No manual SSMS table creation is required.
