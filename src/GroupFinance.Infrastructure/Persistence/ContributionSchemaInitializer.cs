using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

public static class ContributionSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        // Phase 7 evolves the Phase 2 contribution foundation. Each schema change is kept
        // in its own batch so SQL Server can compile newly-added columns safely.
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ContributionPlans', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.ContributionPlans', N'IntervalMonths') IS NULL
    ALTER TABLE dbo.ContributionPlans ADD IntervalMonths int NULL;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH(N'dbo.ContributionPlans', N'IntervalMonths') IS NOT NULL
    UPDATE dbo.ContributionPlans
    SET IntervalMonths = CASE LOWER(LTRIM(RTRIM(Frequency)))
        WHEN 'monthly' THEN 1
        WHEN 'quarterly' THEN 3
        WHEN 'half-yearly' THEN 6
        WHEN 'yearly' THEN 12
        ELSE 1 END
    WHERE IntervalMonths IS NULL OR IntervalMonths <= 0;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH(N'dbo.ContributionPlans', N'IntervalMonths') IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ContributionPlans') AND name = N'IntervalMonths' AND is_nullable = 1)
    ALTER TABLE dbo.ContributionPlans ALTER COLUMN IntervalMonths int NOT NULL;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ContributionPayments', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.ContributionPayments', N'FinancialAccountId') IS NULL
    ALTER TABLE dbo.ContributionPayments ADD FinancialAccountId bigint NULL;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ContributionPayments', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.ContributionPayments', N'FinancialAccountId') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_ContributionPayments_FinancialAccounts'
      AND parent_object_id = OBJECT_ID(N'dbo.ContributionPayments')
)
    ALTER TABLE dbo.ContributionPayments ADD CONSTRAINT FK_ContributionPayments_FinancialAccounts
        FOREIGN KEY (FinancialAccountId) REFERENCES dbo.FinancialAccounts(Id);
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ContributionPayments', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.ContributionPayments', N'FinancialAccountId') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ContributionPayments_FinancialAccountId'
      AND object_id = OBJECT_ID(N'dbo.ContributionPayments')
)
    CREATE INDEX IX_ContributionPayments_FinancialAccountId ON dbo.ContributionPayments(FinancialAccountId);
""", cancellationToken);


        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ContributionPayments', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.ContributionPayments', N'ReferenceNumber') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ContributionPayments_Schedule_Reference_Posted'
      AND object_id = OBJECT_ID(N'dbo.ContributionPayments')
)
    CREATE UNIQUE INDEX UX_ContributionPayments_Schedule_Reference_Posted
        ON dbo.ContributionPayments(ContributionScheduleId, ReferenceNumber)
        WHERE ReferenceNumber IS NOT NULL AND Status = 'Posted';
""", cancellationToken);

        logger.LogInformation("Phase 7 contribution schema initialization completed successfully.");
    }
}
