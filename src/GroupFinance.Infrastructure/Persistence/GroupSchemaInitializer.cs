using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

/// <summary>
/// Evolves an existing Phase 2 database to the Phase 5 single-group setup schema.
/// Fresh databases are created from the EF Core model; this initializer is for existing development databases.
/// </summary>
public static class GroupSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.Groups', 'Code') IS NULL
    ALTER TABLE dbo.Groups ADD Code nvarchar(30) NULL;
IF COL_LENGTH('dbo.Groups', 'Description') IS NULL
    ALTER TABLE dbo.Groups ADD Description nvarchar(1000) NULL;
IF COL_LENGTH('dbo.Groups', 'FormationDate') IS NULL
    ALTER TABLE dbo.Groups ADD FormationDate date NULL;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.Groups', 'Code') IS NOT NULL
BEGIN
    UPDATE dbo.Groups
    SET Code = CONCAT('GRP-', RIGHT('000' + CONVERT(varchar(20), Id), 3))
    WHERE Code IS NULL OR LTRIM(RTRIM(Code)) = '';
END
IF COL_LENGTH('dbo.Groups', 'FormationDate') IS NOT NULL
BEGIN
    UPDATE dbo.Groups
    SET FormationDate = CONVERT(date, CreatedAtUtc)
    WHERE FormationDate IS NULL;
END
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.Groups', 'Code') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Groups WHERE Code IS NULL)
    THROW 51001, 'Existing Groups rows could not be assigned a group code.', 1;
IF COL_LENGTH('dbo.Groups', 'FormationDate') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Groups WHERE FormationDate IS NULL)
    THROW 51002, 'Existing Groups rows could not be assigned a formation date.', 1;

-- SQL Server cannot ALTER a column while an index depends on it.
-- The Phase 2 database may already have the Phase 5 Code index from a previous startup attempt,
-- so remove it before changing Code from nullable to NOT NULL and recreate it afterward.
IF COL_LENGTH('dbo.Groups', 'Code') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Groups') AND name = 'Code' AND is_nullable = 1)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Groups_Code' AND object_id = OBJECT_ID('dbo.Groups'))
        DROP INDEX UX_Groups_Code ON dbo.Groups;
    ALTER TABLE dbo.Groups ALTER COLUMN Code nvarchar(30) NOT NULL;
END

IF COL_LENGTH('dbo.Groups', 'FormationDate') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Groups') AND name = 'FormationDate' AND is_nullable = 1)
    ALTER TABLE dbo.Groups ALTER COLUMN FormationDate date NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Groups_Code' AND object_id = OBJECT_ID('dbo.Groups'))
    CREATE UNIQUE INDEX UX_Groups_Code ON dbo.Groups(Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Groups_Name' AND object_id = OBJECT_ID('dbo.Groups'))
    CREATE UNIQUE INDEX UX_Groups_Name ON dbo.Groups(Name);
""", cancellationToken);

        // Keep each schema-changing step in a separate SQL batch. SQL Server compiles
        // a batch before executing it, so a newly-added column cannot safely be
        // referenced by a later statement in the same batch.
        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.GroupSettings', 'DefaultLoanDuration') IS NULL
    ALTER TABLE dbo.GroupSettings ADD DefaultLoanDuration int NULL;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.GroupSettings', 'DefaultLoanDuration') IS NOT NULL
    UPDATE dbo.GroupSettings
    SET DefaultLoanDuration = 12
    WHERE DefaultLoanDuration IS NULL;
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.GroupSettings', 'DefaultLoanDuration') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GroupSettings') AND name = 'DefaultLoanDuration' AND system_type_id = 106)
BEGIN
    ALTER TABLE dbo.GroupSettings ALTER COLUMN DefaultLoanDuration int NULL;
END
""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.GroupSettings', 'DefaultLoanDuration') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GroupSettings') AND name = 'DefaultLoanDuration' AND is_nullable = 1)
    ALTER TABLE dbo.GroupSettings ALTER COLUMN DefaultLoanDuration int NOT NULL;
""", cancellationToken);

        logger.LogInformation("Phase 5 group schema initialization completed successfully.");
    }
}
