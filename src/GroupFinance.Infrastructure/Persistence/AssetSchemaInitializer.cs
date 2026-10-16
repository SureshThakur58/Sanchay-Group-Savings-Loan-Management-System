using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

public static class AssetSchemaInitializer
{
    public static async Task InitializeAsync(
        GroupFinanceDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var locationExists = await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN COL_LENGTH('dbo.Assets', 'Location') IS NULL THEN 0 ELSE 1 END AS [Value]")
                .SingleAsync(cancellationToken);

            if (locationExists == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE dbo.Assets ADD Location nvarchar(500) NULL;",
                    cancellationToken);
            }

            var descriptionExists = await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN COL_LENGTH('dbo.Assets', 'Description') IS NULL THEN 0 ELSE 1 END AS [Value]")
                .SingleAsync(cancellationToken);

            if (descriptionExists == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE dbo.Assets ADD Description nvarchar(1000) NULL;",
                    cancellationToken);
            }

            var assetTransactionFinancialTransactionExists = await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN COL_LENGTH('dbo.AssetTransactions', 'FinancialTransactionId') IS NULL THEN 0 ELSE 1 END AS [Value]")
                .SingleAsync(cancellationToken);

            if (assetTransactionFinancialTransactionExists == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE dbo.AssetTransactions ADD FinancialTransactionId bigint NULL;",
                    cancellationToken);
            }

            var fkExists = await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetTransactions_FinancialTransactions') THEN 1 ELSE 0 END AS [Value]")
                .SingleAsync(cancellationToken);

            if (fkExists == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE dbo.AssetTransactions ADD CONSTRAINT FK_AssetTransactions_FinancialTransactions FOREIGN KEY (FinancialTransactionId) REFERENCES dbo.FinancialTransactions(Id);",
                    cancellationToken);
            }

            var indexExists = await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetTransactions_FinancialTransactionId' AND object_id = OBJECT_ID('dbo.AssetTransactions')) THEN 1 ELSE 0 END AS [Value]")
                .SingleAsync(cancellationToken);

            if (indexExists == 0)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IX_AssetTransactions_FinancialTransactionId ON dbo.AssetTransactions(FinancialTransactionId);",
                    cancellationToken);
            }

            logger.LogInformation("Asset Management schema initialization completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Asset Management schema initialization failed.");
            throw;
        }
    }
}
