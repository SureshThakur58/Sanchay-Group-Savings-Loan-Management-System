using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

public static class MemberSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        // Existing Phase 2/5 databases already contain Members. Phase 6 adds the PRD master fields
        // that were not present in the original foundation schema. Keep each ALTER in its own batch
        // so SQL Server compiles newly added columns independently.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Members', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.Members', N'Email') IS NULL
    ALTER TABLE dbo.Members ADD Email nvarchar(254) NULL;", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Members', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.Members', N'DateOfBirth') IS NULL
    ALTER TABLE dbo.Members ADD DateOfBirth datetime2(0) NULL;", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Members', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.Members', N'Notes') IS NULL
    ALTER TABLE dbo.Members ADD Notes nvarchar(2000) NULL;", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Members', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Members_Email'
      AND object_id = OBJECT_ID(N'dbo.Members')
)
    CREATE INDEX IX_Members_Email ON dbo.Members(Email);", cancellationToken);

        logger.LogInformation("Member Management schema initialization completed.");
    }
}
