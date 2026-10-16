using System.Data;
using System.Text.Json;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Audit;

public sealed class ReversalService(
    GroupFinanceDbContext db,
    FinancialTransactionService financialTransactions)
{
    public async Task<FinancialTransactionResponse> ReverseAsync(
        long transactionId,
        ReverseFinancialTransactionRequest request,
        long userId,
        string? ipAddress,
        CancellationToken ct)
    {
        if (transactionId <= 0) throw new AuditValidationException("transactionId is required.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new AuditValidationException("A reversal reason is required.");
        if (request.Reason.Trim().Length > 1000)
            throw new AuditValidationException("Reversal reason cannot exceed 1000 characters.");
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber) && request.ReferenceNumber.Trim().Length > 100)
            throw new AuditValidationException("Reference number cannot exceed 100 characters.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var original = await db.FinancialTransactions
                .Include(x => x.Details)
                    .ThenInclude(x => x.FinancialAccount)
                .SingleOrDefaultAsync(x => x.Id == transactionId, ct)
                ?? throw new AuditValidationException("Financial transaction not found.");

            if (!string.Equals(original.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new AuditValidationException("Only posted financial transactions can be reversed.");

            if (original.ReversalOfTransactionId.HasValue)
                throw new AuditValidationException("A reversal transaction cannot itself be reversed.");

            var alreadyReversed = await db.FinancialTransactions.AnyAsync(
                x => x.ReversalOfTransactionId == original.Id, ct);
            if (alreadyReversed)
                throw new AuditValidationException("The selected financial transaction has already been reversed.");


            var reversalDate = (request.ReversalDate ?? DateTime.UtcNow.Date).Date;
            var lines = original.Details
                .OrderBy(x => x.Id)
                .Select(x => new FinancialTransactionLineRequest(
                    x.FinancialAccountId,
                    x.CreditAmount,
                    x.DebitAmount,
                    $"Reversal of {original.TransactionNumber}: {x.Description}"))
                .ToArray();

            var reason = request.Reason.Trim();
            var reference = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"REV-{original.TransactionNumber}"
                : request.ReferenceNumber.Trim();

            // The original is made non-posted before the counter-journal is created.
            // The entire operation is atomic, so a failure rolls this change back.
            var oldStatus = original.Status;
            original.Status = "Reversed";
            original.ModifiedAtUtc = DateTime.UtcNow;
            original.ModifiedByUserId = userId;

            var reversal = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    original.GroupId,
                    original.MemberId,
                    reversalDate,
                    FinancialTransactionTypes.Adjustment,
                    $"Reversal of {original.TransactionNumber}: {reason}",
                    "REVERSAL",
                    original.Id,
                    reference,
                    lines),
                userId,
                ct);

            var audit = new AuditLog
            {
                UserId = userId,
                EntityName = "FinancialTransaction",
                EntityId = original.Id,
                Action = "Reversed",
                OldValuesJson = JsonSerializer.Serialize(new
                {
                    Status = oldStatus,
                    TransactionNumber = original.TransactionNumber,
                    Amount = original.Amount,
                    TotalDebit = original.TotalDebit,
                    TotalCredit = original.TotalCredit
                }),
                NewValuesJson = JsonSerializer.Serialize(new
                {
                    Status = original.Status,
                    ReversalTransactionId = reversal.Id,
                    ReversalTransactionNumber = reversal.TransactionNumber,
                    ReversalAmount = reversal.Amount
                }),
                Reason = reason,
                IpAddress = ipAddress,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };
            db.AuditLogs.Add(audit);

            // Link the reversal to the original after the central service has created it.
            var reversalEntity = await db.FinancialTransactions
                .SingleAsync(x => x.Id == reversal.Id, ct);
            reversalEntity.ReversalOfTransactionId = original.Id;
            reversalEntity.ModifiedAtUtc = DateTime.UtcNow;
            reversalEntity.ModifiedByUserId = userId;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return await financialTransactions.GetAsync(reversal.Id, ct)
                ?? throw new InvalidOperationException("The reversal was created but could not be reloaded.");
        });
    }
}
