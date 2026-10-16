using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Receipts;

public sealed class ReceiptService(GroupFinanceDbContext db)
{
    public async Task<ReceiptResponse?> GetAsync(long transactionId, CancellationToken ct)
    {
        if (transactionId <= 0)
            throw new ReceiptValidationException("transactionId is required.");

        var transaction = await db.FinancialTransactions
            .AsNoTracking()
            .Include(x => x.Group)
            .Include(x => x.Member)
            .Include(x => x.Details)
                .ThenInclude(x => x.FinancialAccount)
            .SingleOrDefaultAsync(x => x.Id == transactionId, ct);

        if (transaction is null)
            return null;

        if (!string.Equals(transaction.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            throw new ReceiptValidationException("Receipts can only be generated for posted financial transactions.");

        var receiptType = ReceiptTypeResolver.Resolve(transaction.TransactionType)
            ?? throw new ReceiptValidationException(
                $"Receipts are not supported for transaction type '{transaction.TransactionType}'.");

        var preparedBy = transaction.CreatedByUserId.HasValue
            ? await db.Users.AsNoTracking()
                .Where(x => x.Id == transaction.CreatedByUserId.Value)
                .Select(x => x.FullName)
                .SingleOrDefaultAsync(ct)
            : null;

        var paymentMethod = await ResolvePaymentMethodAsync(transaction, ct);

        var memberId = transaction.MemberId;
        var memberNumber = transaction.Member?.MemberNumber;
        var memberName = transaction.Member?.FullName;
        if (string.Equals(transaction.TransactionType, "INTEREST_DISTRIBUTION", StringComparison.OrdinalIgnoreCase))
        {
            memberId = null;
            memberNumber = null;
            memberName = "Multiple members";
        }

        var lines = transaction.Details
            .OrderBy(x => x.Id)
            .Select(x => new ReceiptLineResponse(
                x.FinancialAccount.AccountCode,
                x.FinancialAccount.AccountName,
                x.DebitAmount,
                x.CreditAmount,
                x.Description))
            .ToArray();

        return new ReceiptResponse(
            transaction.Id,
            receiptType,
            transaction.GroupId,
            transaction.Group.Code,
            transaction.Group.Name,
            memberId,
            memberNumber,
            memberName,
            transaction.TransactionNumber,
            transaction.TransactionDate,
            transaction.Amount,
            paymentMethod,
            transaction.Description,
            transaction.ReferenceNumber,
            string.IsNullOrWhiteSpace(preparedBy) ? "System" : preparedBy,
            transaction.Status,
            lines);
    }

    private async Task<string?> ResolvePaymentMethodAsync(FinancialTransaction transaction, CancellationToken ct)
    {
        var type = transaction.TransactionType.Trim().ToUpperInvariant();

        switch (type)
        {
            case "CONTRIBUTION":
                if (transaction.ReferenceId.HasValue)
                {
                    var method = await db.ContributionPayments.AsNoTracking()
                        .Where(x => x.ContributionScheduleId == transaction.ReferenceId.Value &&
                                    x.FinancialTransactionId == transaction.Id)
                        .Select(x => x.PaymentMethod)
                        .SingleOrDefaultAsync(ct);
                    if (!string.IsNullOrWhiteSpace(method))
                        return method;
                }
                break;

            case "LOAN_REPAYMENT":
                if (transaction.ReferenceId.HasValue)
                {
                    var method = await db.LoanPayments.AsNoTracking()
                        .Where(x => x.Id == transaction.ReferenceId.Value &&
                                    x.FinancialTransactionId == transaction.Id)
                        .Select(x => x.PaymentMethod)
                        .SingleOrDefaultAsync(ct);
                    if (!string.IsNullOrWhiteSpace(method))
                        return method;
                }
                break;

            case "EXPENSE":
                if (transaction.ReferenceId.HasValue)
                {
                    var method = await db.Expenses.AsNoTracking()
                        .Where(x => x.Id == transaction.ReferenceId.Value &&
                                    x.FinancialTransactionId == transaction.Id)
                        .Select(x => x.PaymentMethod)
                        .SingleOrDefaultAsync(ct);
                    if (!string.IsNullOrWhiteSpace(method))
                        return method;
                }
                break;

            case "LOAN_DISBURSEMENT":
            case "ASSET_PURCHASE":
                // These entities do not persist a payment-method field.
                // Derive it from the credited fund account.
                var credit = transaction.Details
                    .Where(x => x.CreditAmount > 0m)
                    .OrderBy(x => x.Id)
                    .Select(x => x.FinancialAccount.AccountType)
                    .FirstOrDefault();

                return credit switch
                {
                    "Cash" => "Cash",
                    "Bank" => "Bank",
                    "Other" => "Other",
                    _ => null
                };

            case "INTEREST_DISTRIBUTION":
                return "Not Applicable";
        }

        return null;
    }
}

public sealed class ReceiptValidationException(string message) : Exception(message);
