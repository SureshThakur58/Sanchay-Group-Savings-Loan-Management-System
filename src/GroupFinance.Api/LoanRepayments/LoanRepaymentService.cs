using System.Data;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Core.Loans;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.LoanRepayments;

public sealed class LoanRepaymentValidationException(string message) : Exception(message);

public sealed class LoanRepaymentService(
    GroupFinanceDbContext db,
    FinancialTransactionService financialTransactions)
{
    private static readonly string[] FundAccountTypes = ["Cash", "Bank", "Other"];
    private static readonly string[] PaymentMethods = ["Cash", "Bank", "Online Transfer", "Other"];

    public async Task<LoanRepaymentResponse> RepayAsync(long loanId, LoanRepaymentRequest request, long userId, CancellationToken ct)
    {
        if (loanId <= 0) throw new LoanRepaymentValidationException("LoanId is required.");
        if (request.FinancialAccountId <= 0) throw new LoanRepaymentValidationException("A repayment financial account is required.");
        if (request.TransactionDate == default) throw new LoanRepaymentValidationException("Transaction date is required.");
        if (request.Amount <= 0m) throw new LoanRepaymentValidationException("Repayment amount must be greater than zero.");

        var paymentMethod = request.PaymentMethod?.Trim() ?? string.Empty;
        if (!PaymentMethods.Contains(paymentMethod, StringComparer.OrdinalIgnoreCase))
            throw new LoanRepaymentValidationException("Payment method must be Cash, Bank, Online Transfer, or Other.");
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber) && request.ReferenceNumber.Trim().Length > 100)
            throw new LoanRepaymentValidationException("Reference number cannot exceed 100 characters.");
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 500)
            throw new LoanRepaymentValidationException("Description cannot exceed 500 characters.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var loan = await db.Loans
                .Include(x => x.Member).ThenInclude(x => x.Group)
                .Include(x => x.Schedules.OrderBy(s => s.InstallmentNumber))
                .SingleOrDefaultAsync(x => x.Id == loanId, ct);

            if (loan is null) throw new LoanRepaymentValidationException("Loan not found.");
            if (loan.Status != "Active") throw new LoanRepaymentValidationException("Only Active loans can receive repayments.");

            // ReferenceNumber is the external request/payment reference when supplied.
            // Treat a repeated reference on the same loan as a duplicate submission so
            // browser retries or double-clicks cannot create a second financial posting.
            var referenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
            if (referenceNumber is not null && await db.LoanPayments.AnyAsync(
                    x => x.LoanId == loanId &&
                         x.ReferenceNumber == referenceNumber &&
                         x.Status == "Posted", ct))
                throw new LoanRepaymentValidationException("A posted loan repayment already exists with this reference number.");
            if (!loan.Member.IsActive) throw new LoanRepaymentValidationException("The loan member is inactive.");
            if (request.TransactionDate.Date < loan.DisbursementDate.Date)
                throw new LoanRepaymentValidationException("Repayment date cannot be earlier than the loan disbursement date.");

            var settings = await db.GroupSettings.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == loan.GroupId, ct)
                ?? throw new LoanRepaymentValidationException("The configured group does not have loan settings.");

            var sourceAccount = await db.FinancialAccounts.SingleOrDefaultAsync(
                x => x.Id == request.FinancialAccountId && x.GroupId == loan.GroupId && x.IsActive, ct)
                ?? throw new LoanRepaymentValidationException("The selected repayment account does not exist, is inactive, or belongs to another group.");
            if (!FundAccountTypes.Contains(sourceAccount.AccountType, StringComparer.OrdinalIgnoreCase))
                throw new LoanRepaymentValidationException("The repayment account must be a Cash, Bank, or Other fund account.");

            var sourceBalance = await GetAccountBalanceAsync(sourceAccount.Id, ct);
            var amount = RoundMoney(request.Amount, settings.MonetaryPrecision);
            if (sourceBalance < amount)
                throw new LoanRepaymentValidationException($"Insufficient funds in the selected account. Available balance is {sourceBalance:N2}, but the repayment requires {amount:N2}.");

            var activeSchedules = loan.Schedules
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.InstallmentNumber)
                .Where(x => x.PrincipalPaid < x.PrincipalDue || x.InterestPaid < x.InterestDue || x.PenaltyPaid < x.PenaltyDue)
                .ToList();

            var allocationResult = LoanRepaymentAllocationCalculator.Calculate(
                amount,
                request.TransactionDate,
                activeSchedules.Select(x => new LoanRepaymentAllocationInput(
                    x.Id, x.InstallmentNumber, x.DueDate,
                    Math.Max(0m, x.PrincipalDue - x.PrincipalPaid),
                    Math.Max(0m, x.InterestDue - x.InterestPaid),
                    Math.Max(0m, x.PenaltyDue - x.PenaltyPaid))).ToArray());

            var penaltyAllocated = allocationResult.PenaltyAllocated;
            var interestAllocated = allocationResult.InterestAllocated;
            var principalAllocated = allocationResult.PrincipalAllocated;
            var allocations = allocationResult.Allocations
                .Select(x => new LoanRepaymentAllocationResponse(x.ScheduleId, x.InstallmentNumber, x.DueDate, x.PenaltyAllocated, x.InterestAllocated, x.PrincipalAllocated))
                .ToList();

            foreach (var allocation in allocationResult.Allocations)
            {
                var schedule = loan.Schedules.Single(x => x.Id == allocation.ScheduleId);
                schedule.PenaltyPaid += allocation.PenaltyAllocated;
                schedule.InterestPaid += allocation.InterestAllocated;
                schedule.PrincipalPaid += allocation.PrincipalAllocated;
                UpdateScheduleStatus(schedule, request.TransactionDate, userId);
            }

            var overpayment = RoundMoney(allocationResult.OverpaymentAmount, settings.MonetaryPrecision);
            // If the customer pays more than the entire remaining principal/interest/penalty,
            // retain the excess as an overpayment liability instead of creating a negative loan.
            var now = DateTime.UtcNow;
            if (overpayment > 0m)
                loan.OverpaymentAmount += overpayment;

            loan.PrincipalPaid = RoundMoney(loan.PrincipalPaid + principalAllocated, settings.MonetaryPrecision);
            loan.InterestPaid = RoundMoney(loan.InterestPaid + interestAllocated, settings.MonetaryPrecision);
            loan.PenaltyPaid = RoundMoney(loan.PenaltyPaid + penaltyAllocated, settings.MonetaryPrecision);
            loan.ModifiedAtUtc = now;
            loan.ModifiedByUserId = userId;

            var outstandingPrincipal = loan.Schedules.Sum(x => Math.Max(0m, x.PrincipalDue - x.PrincipalPaid));
            var currentlyDueCharges = loan.Schedules
                .Where(x => x.DueDate.Date <= request.TransactionDate.Date)
                .Sum(x => Math.Max(0m, x.InterestDue - x.InterestPaid) + Math.Max(0m, x.PenaltyDue - x.PenaltyPaid));
            if (outstandingPrincipal <= 0m && currentlyDueCharges <= 0m)
            {
                // Full prepayment clears future unearned interest. Future interest is not
                // charged once the principal is fully settled.
                foreach (var futureSchedule in loan.Schedules.Where(x => x.DueDate.Date > request.TransactionDate.Date))
                {
                    futureSchedule.InterestDue = 0m;
                    futureSchedule.TotalDue = RoundMoney(Math.Max(0m, futureSchedule.PrincipalDue - futureSchedule.PrincipalPaid) + Math.Max(0m, futureSchedule.PenaltyDue - futureSchedule.PenaltyPaid), settings.MonetaryPrecision);
                    if (futureSchedule.PrincipalPaid >= futureSchedule.PrincipalDue && futureSchedule.PenaltyPaid >= futureSchedule.PenaltyDue)
                        futureSchedule.Status = "Paid";
                    futureSchedule.ModifiedAtUtc = now;
                    futureSchedule.ModifiedByUserId = userId;
                }
                loan.Status = "Closed";
                loan.ClosedDate = request.TransactionDate.Date;
            }

            var payment = new LoanPayment
            {
                LoanId = loan.Id,
                LoanScheduleId = allocations.Count == 1 ? allocations[0].LoanScheduleId : null,
                TransactionDate = request.TransactionDate.Date,
                Amount = amount,
                PenaltyAllocated = penaltyAllocated,
                InterestAllocated = interestAllocated,
                PrincipalAllocated = principalAllocated,
                OverpaymentAmount = overpayment,
                PaymentMethod = CanonicalPaymentMethod(paymentMethod),
                ReferenceNumber = referenceNumber,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Status = "Posted",
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };
            db.LoanPayments.Add(payment);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                throw new LoanRepaymentValidationException("A posted loan repayment already exists with this reference number.");
            }

            var loanReceivable = await EnsureSystemAccountAsync(loan.GroupId, "LOAN-RECEIVABLE", "Member Loan Receivable", "Asset", userId, ct);
            var interestAccount = interestAllocated > 0m
                ? await EnsureSystemAccountAsync(loan.GroupId, "LOAN-INTEREST-INCOME", "Loan Interest Income", "Income", userId, ct)
                : null;
            var penaltyAccount = penaltyAllocated > 0m
                ? await EnsureSystemAccountAsync(loan.GroupId, "LOAN-PENALTY-INCOME", "Loan Penalty Income", "Income", userId, ct)
                : null;
            var overpaymentAccount = overpayment > 0m
                ? await EnsureSystemAccountAsync(loan.GroupId, "LOAN-OVERPAYMENT", "Customer Loan Overpayments", "Liability", userId, ct)
                : null;

            var lines = new List<FinancialTransactionLineRequest>
            {
                new(sourceAccount.Id, amount, 0m, "Loan repayment received")
            };
            if (principalAllocated > 0m) lines.Add(new FinancialTransactionLineRequest(loanReceivable.Id, 0m, principalAllocated, "Loan principal repayment"));
            if (interestAllocated > 0m && interestAccount is not null) lines.Add(new FinancialTransactionLineRequest(interestAccount.Id, 0m, interestAllocated, "Loan interest income"));
            if (penaltyAllocated > 0m && penaltyAccount is not null) lines.Add(new FinancialTransactionLineRequest(penaltyAccount.Id, 0m, penaltyAllocated, "Loan penalty income"));
            if (overpayment > 0m && overpaymentAccount is not null) lines.Add(new FinancialTransactionLineRequest(overpaymentAccount.Id, 0m, overpayment, "Customer loan overpayment held"));

            var journal = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    loan.GroupId,
                    loan.MemberId,
                    request.TransactionDate.Date,
                    FinancialTransactionTypes.LoanRepayment,
                    string.IsNullOrWhiteSpace(request.Description) ? $"Loan repayment {loan.LoanNumber}" : request.Description.Trim(),
                    "LOAN_REPAYMENT",
                    payment.Id,
                    referenceNumber,
                    lines), userId, ct);

            payment.FinancialTransactionId = journal.Id;
            payment.ModifiedAtUtc = now;
            payment.ModifiedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new LoanRepaymentResponse(
                payment.Id, loan.Id, loan.LoanNumber, loan.MemberId, payment.Amount,
                payment.PenaltyAllocated, payment.InterestAllocated, payment.PrincipalAllocated,
                payment.OverpaymentAmount, payment.PaymentMethod, payment.TransactionDate,
                payment.Status, journal.Id, journal.TransactionNumber, sourceAccount.Id,
                sourceAccount.AccountCode, sourceAccount.AccountName, loan.PrincipalPaid,
                loan.InterestPaid, loan.PenaltyPaid,
                Math.Max(0m, loan.PrincipalAmount - loan.PrincipalPaid), loan.Status,
                allocations);
        });
    }

    public async Task<IReadOnlyCollection<LoanPaymentSummaryResponse>> ListAsync(long loanId, CancellationToken ct)
    {
        if (!await db.Loans.AnyAsync(x => x.Id == loanId, ct)) throw new LoanRepaymentValidationException("Loan not found.");
        return await db.LoanPayments.AsNoTracking()
            .Where(x => x.LoanId == loanId)
            .OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id)
            .Take(500)
            .Select(x => new LoanPaymentSummaryResponse(
                x.Id, x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId, x.TransactionDate, x.Amount,
                x.PenaltyAllocated, x.InterestAllocated, x.PrincipalAllocated, x.OverpaymentAmount,
                x.PaymentMethod, x.ReferenceNumber, x.Description, x.Status, x.FinancialTransactionId))
            .ToArrayAsync(ct);
    }

    public async Task<LoanPaymentSummaryResponse?> GetPaymentAsync(long paymentId, CancellationToken ct)
    {
        return await db.LoanPayments.AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Select(x => new LoanPaymentSummaryResponse(
                x.Id, x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId, x.TransactionDate, x.Amount,
                x.PenaltyAllocated, x.InterestAllocated, x.PrincipalAllocated, x.OverpaymentAmount,
                x.PaymentMethod, x.ReferenceNumber, x.Description, x.Status, x.FinancialTransactionId))
            .SingleOrDefaultAsync(ct);
    }

    private async Task<FinancialAccount> EnsureSystemAccountAsync(long groupId, string code, string name, string type, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == code, ct);
        if (account is not null)
        {
            if (!account.IsActive || !string.Equals(account.AccountType, type, StringComparison.OrdinalIgnoreCase))
                throw new LoanRepaymentValidationException($"The system account {code} is inactive or has an invalid account type.");
            return account;
        }
        account = new FinancialAccount
        {
            GroupId = groupId, AccountCode = code, AccountName = name, AccountType = type,
            IsSystemAccount = true, IsActive = true, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = userId
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<decimal> GetAccountBalanceAsync(long accountId, CancellationToken ct)
    {
        var totals = await db.TransactionDetails
            .Where(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted")
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .SingleOrDefaultAsync(ct);
        return (totals?.Debit ?? 0m) - (totals?.Credit ?? 0m);
    }

    private static void UpdateScheduleStatus(LoanSchedule schedule, DateTime paymentDate, long userId)
    {
        var outstanding = Math.Max(0m, schedule.PrincipalDue - schedule.PrincipalPaid) +
                          Math.Max(0m, schedule.InterestDue - schedule.InterestPaid) +
                          Math.Max(0m, schedule.PenaltyDue - schedule.PenaltyPaid);
        schedule.Status = outstanding <= 0m ? "Paid" :
            (schedule.PrincipalPaid > 0m || schedule.InterestPaid > 0m || schedule.PenaltyPaid > 0m ? "PartiallyPaid" : schedule.Status);
        schedule.ModifiedAtUtc = DateTime.UtcNow;
        schedule.ModifiedByUserId = userId;
    }

    private static string CanonicalPaymentMethod(string method) => method switch
    {
        var x when x.Equals("Cash", StringComparison.OrdinalIgnoreCase) => "Cash",
        var x when x.Equals("Bank", StringComparison.OrdinalIgnoreCase) => "Bank",
        var x when x.Equals("Online Transfer", StringComparison.OrdinalIgnoreCase) => "Online Transfer",
        _ => "Other"
    };

    private static decimal RoundMoney(decimal value, int precision) => decimal.Round(value, precision, MidpointRounding.AwayFromZero);
}
