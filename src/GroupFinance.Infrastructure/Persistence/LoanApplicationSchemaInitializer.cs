using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

public static class LoanApplicationSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.LoanApplications', N'LoanApplicationNumber') IS NULL
    ALTER TABLE dbo.LoanApplications ADD LoanApplicationNumber nvarchar(30) NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.LoanApplications', N'ProposedInterestMethod') IS NULL
    ALTER TABLE dbo.LoanApplications ADD ProposedInterestMethod nvarchar(30) NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.LoanApplications', N'RequestedEmi') IS NULL
    ALTER TABLE dbo.LoanApplications ADD RequestedEmi decimal(18,2) NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.LoanApplications
    SET LoanApplicationNumber = 'LA-' + RIGHT('00000000' + CONVERT(varchar(20), Id), 8)
    WHERE LoanApplicationNumber IS NULL OR LTRIM(RTRIM(LoanApplicationNumber)) = '';
    UPDATE dbo.LoanApplications
    SET ProposedInterestMethod = 'ReducingBalance'
    WHERE ProposedInterestMethod IS NULL OR LTRIM(RTRIM(ProposedInterestMethod)) = '';
    UPDATE dbo.LoanApplications SET RequestedEmi = 0 WHERE RequestedEmi IS NULL;
    UPDATE dbo.LoanApplications SET Status = 'Draft' WHERE Status = 'Pending';
END;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LoanApplications') AND name = N'LoanApplicationNumber' AND is_nullable = 1)
    ALTER TABLE dbo.LoanApplications ALTER COLUMN LoanApplicationNumber nvarchar(30) NOT NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LoanApplications') AND name = N'ProposedInterestMethod' AND is_nullable = 1)
    ALTER TABLE dbo.LoanApplications ALTER COLUMN ProposedInterestMethod nvarchar(30) NOT NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LoanApplications') AND name = N'RequestedEmi' AND is_nullable = 1)
    ALTER TABLE dbo.LoanApplications ALTER COLUMN RequestedEmi decimal(18,2) NOT NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanApplications', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoanApplications_LoanApplicationNumber' AND object_id = OBJECT_ID(N'dbo.LoanApplications'))
    CREATE UNIQUE INDEX IX_LoanApplications_LoanApplicationNumber ON dbo.LoanApplications(LoanApplicationNumber);
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanGuarantors', N'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LoanGuarantors') AND name = N'LoanId' AND is_nullable = 0)
    ALTER TABLE dbo.LoanGuarantors ALTER COLUMN LoanId bigint NULL;
""", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanGuarantors', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoanGuarantors_LoanId_MemberId' AND object_id = OBJECT_ID(N'dbo.LoanGuarantors'))
        DROP INDEX IX_LoanGuarantors_LoanId_MemberId ON dbo.LoanGuarantors;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoanGuarantors_LoanId_MemberId' AND object_id = OBJECT_ID(N'dbo.LoanGuarantors'))
        CREATE UNIQUE INDEX IX_LoanGuarantors_LoanId_MemberId ON dbo.LoanGuarantors(LoanId, MemberId) WHERE LoanId IS NOT NULL AND Status = 'Active';

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoanGuarantors_LoanApplicationId_MemberId' AND object_id = OBJECT_ID(N'dbo.LoanGuarantors'))
        DROP INDEX IX_LoanGuarantors_LoanApplicationId_MemberId ON dbo.LoanGuarantors;

    CREATE UNIQUE INDEX IX_LoanGuarantors_LoanApplicationId_MemberId ON dbo.LoanGuarantors(LoanApplicationId, MemberId) WHERE LoanApplicationId IS NOT NULL AND Status = 'Active';
END;
""", cancellationToken);
        logger.LogInformation("Phase 10 loan application schema initialization completed successfully.");
    }
}
