using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

/// <summary>
/// Upgrades the Phase 2 FinancialTransactions table in-place through the API.
/// This is needed because Phase 2 databases may have been created with EnsureCreated
/// before the Phase 4 transaction metadata was introduced.
/// </summary>
public static class FinancialTransactionSchemaInitializer
{
    public static async Task InitializeAsync(
        GroupFinanceDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // IMPORTANT: Keep schema-changing statements in separate SQL batches.
        // SQL Server can compile the whole raw-SQL batch before executing the ALTER TABLE
        // statements. If a later statement references a column that an earlier statement
        // adds, SQL Server may therefore report "Invalid column name" for a brand-new column.
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.FinancialTransactions', N'U') IS NULL
    THROW 51001, 'FinancialTransactions table does not exist. Phase 2 database foundation must be initialized first.', 1;
""", cancellationToken);

        // Phase 2 -> Phase 4 columns. This is its own batch so all subsequent SQL can
        // safely compile against the newly added columns.
        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH(N'dbo.FinancialTransactions', N'GroupId') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD GroupId bigint NULL;

IF COL_LENGTH(N'dbo.FinancialTransactions', N'MemberId') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD MemberId bigint NULL;

IF COL_LENGTH(N'dbo.FinancialTransactions', N'Amount') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD Amount decimal(18,2) NOT NULL CONSTRAINT DF_FinancialTransactions_Amount DEFAULT (0);

IF COL_LENGTH(N'dbo.FinancialTransactions', N'TotalDebit') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD TotalDebit decimal(18,2) NOT NULL CONSTRAINT DF_FinancialTransactions_TotalDebit DEFAULT (0);

IF COL_LENGTH(N'dbo.FinancialTransactions', N'TotalCredit') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD TotalCredit decimal(18,2) NOT NULL CONSTRAINT DF_FinancialTransactions_TotalCredit DEFAULT (0);

IF COL_LENGTH(N'dbo.FinancialTransactions', N'ReferenceType') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD ReferenceType nvarchar(100) NULL;

IF COL_LENGTH(N'dbo.FinancialTransactions', N'ReferenceId') IS NULL
    ALTER TABLE dbo.FinancialTransactions ADD ReferenceId bigint NULL;
""", cancellationToken);

        // Indexes and foreign keys are in a later batch so SQL Server compiles them only
        // after GroupId/MemberId definitely exist.
        await db.Database.ExecuteSqlRawAsync("""
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinancialTransactions') AND name = N'IX_FinancialTransactions_GroupId_TransactionDate_TransactionType')
    CREATE INDEX IX_FinancialTransactions_GroupId_TransactionDate_TransactionType ON dbo.FinancialTransactions(GroupId, TransactionDate, TransactionType);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinancialTransactions') AND name = N'IX_FinancialTransactions_GroupId_MemberId_TransactionDate')
    CREATE INDEX IX_FinancialTransactions_GroupId_MemberId_TransactionDate ON dbo.FinancialTransactions(GroupId, MemberId, TransactionDate);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinancialTransactions_Groups' AND parent_object_id = OBJECT_ID(N'dbo.FinancialTransactions'))
   AND NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WHERE GroupId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Groups g WHERE g.Id = GroupId))
BEGIN
    ALTER TABLE dbo.FinancialTransactions WITH CHECK ADD CONSTRAINT FK_FinancialTransactions_Groups FOREIGN KEY(GroupId) REFERENCES dbo.Groups(Id);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinancialTransactions_Members' AND parent_object_id = OBJECT_ID(N'dbo.FinancialTransactions'))
   AND NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions ft WHERE ft.MemberId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Members m WHERE m.Id = ft.MemberId))
BEGIN
    ALTER TABLE dbo.FinancialTransactions WITH CHECK ADD CONSTRAINT FK_FinancialTransactions_Members FOREIGN KEY(MemberId) REFERENCES dbo.Members(Id);
END;
""", cancellationToken);

        // Constraints are deliberately added after the columns exist.
        await db.Database.ExecuteSqlRawAsync("""
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_FinancialTransactions_Balanced' AND parent_object_id = OBJECT_ID(N'dbo.FinancialTransactions'))
    ALTER TABLE dbo.FinancialTransactions WITH NOCHECK ADD CONSTRAINT CK_FinancialTransactions_Balanced CHECK ([TotalDebit] = [TotalCredit]);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_FinancialTransactions_AmountPositive' AND parent_object_id = OBJECT_ID(N'dbo.FinancialTransactions'))
    ALTER TABLE dbo.FinancialTransactions WITH NOCHECK ADD CONSTRAINT CK_FinancialTransactions_AmountPositive CHECK ([Amount] > 0 AND [TotalDebit] > 0 AND [TotalCredit] > 0);
""", cancellationToken);

        // System setting uses escaped braces because ExecuteSqlRawAsync treats {n} as
        // composite-format placeholders. The database receives the intended {0} format.
        await db.Database.ExecuteSqlRawAsync("""
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = N'FinancialTransactionNumberFormat')
BEGIN
    INSERT INTO dbo.SystemSettings (CreatedAtUtc, SettingKey, SettingValue, Description, IsSensitive)
    VALUES (SYSUTCDATETIME(), N'FinancialTransactionNumberFormat', N'FT-{{0:00000000}}', N'Format string for financial transaction numbers. {{0}} is the transaction ID.', 0);
END;
""", cancellationToken);

        // Existing Phase 2 rows, if any, are backfilled from their detail lines.
        // Phase 4 does not permit posting until GroupId is available.
        await db.Database.ExecuteSqlRawAsync("""
UPDATE ft
SET TotalDebit = totals.TotalDebit,
    TotalCredit = totals.TotalCredit,
    Amount = totals.TotalDebit
FROM dbo.FinancialTransactions ft
CROSS APPLY
(
    SELECT COALESCE(SUM(td.DebitAmount), 0) AS TotalDebit,
           COALESCE(SUM(td.CreditAmount), 0) AS TotalCredit
    FROM dbo.TransactionDetails td
    WHERE td.FinancialTransactionId = ft.Id
) totals
WHERE ft.TotalDebit = 0 AND ft.TotalCredit = 0;
""", cancellationToken);

        logger.LogInformation("Financial transaction foundation schema is ready.");
    }
}
