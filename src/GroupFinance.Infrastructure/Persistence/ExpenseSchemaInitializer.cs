using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

public static class ExpenseSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger)
    {
        await EnsureExpenseNumberColumnAsync(db, logger);
        await EnsureExpenseNumberIndexAsync(db, logger);
    }

    private static async Task EnsureExpenseNumberColumnAsync(GroupFinanceDbContext db, ILogger logger)
    {
        // Keep each schema operation in its own SQL batch. SQL Server compiles a batch
        // before executing it, so an UPDATE that references a column added by an
        // earlier ALTER TABLE in the same batch can still fail with "Invalid column name".
        var columnExists = await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN COL_LENGTH('dbo.Expenses', 'ExpenseNumber') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync();

        if (columnExists == 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE dbo.Expenses ADD ExpenseNumber nvarchar(30) NULL;");
            logger.LogInformation("Added Expenses.ExpenseNumber column.");
        }

        // Run the backfill in a separate batch so the newly-added column is visible
        // to SQL Server's compiler. This is also safe for databases where the column
        // already existed but contains NULL values.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE dbo.Expenses SET ExpenseNumber = 'EXP-' + RIGHT('00000000' + CONVERT(varchar(20), Id), 8) WHERE ExpenseNumber IS NULL;");

        var nullableCount = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM dbo.Expenses WHERE ExpenseNumber IS NULL")
            .SingleAsync();

        if (nullableCount == 0)
        {
            // ALTER COLUMN is intentionally a separate batch as well.
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE dbo.Expenses ALTER COLUMN ExpenseNumber nvarchar(30) NOT NULL;");
        }

        logger.LogInformation("Expense number schema initialization completed.");
    }

    private static async Task EnsureExpenseNumberIndexAsync(GroupFinanceDbContext db, ILogger logger)
    {
        const string sql = """
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Expenses_Group_ExpenseNumber' AND object_id = OBJECT_ID('dbo.Expenses'))
BEGIN
    CREATE UNIQUE INDEX UX_Expenses_Group_ExpenseNumber ON dbo.Expenses(GroupId, ExpenseNumber);
END
""";
        await db.Database.ExecuteSqlRawAsync(sql);
        logger.LogInformation("Expense number unique index initialization completed.");
    }
}
