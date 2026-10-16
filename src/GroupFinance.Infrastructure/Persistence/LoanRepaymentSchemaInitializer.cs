using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Persistence;

public static class LoanRepaymentSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LoanPayments', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.LoanPayments', N'ReferenceNumber') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_LoanPayments_Loan_Reference_Posted'
      AND object_id = OBJECT_ID(N'dbo.LoanPayments')
)
    CREATE UNIQUE INDEX UX_LoanPayments_Loan_Reference_Posted
        ON dbo.LoanPayments(LoanId, ReferenceNumber)
        WHERE ReferenceNumber IS NOT NULL AND Status = 'Posted';
""", cancellationToken);

        logger.LogInformation("Phase 27 loan repayment idempotency schema initialization completed successfully.");
    }
}
