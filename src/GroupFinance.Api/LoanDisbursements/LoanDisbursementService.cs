using System.Data;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Core.Loans;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.LoanDisbursements;

public sealed class LoanDisbursementValidationException(string message) : Exception(message);

public sealed class LoanDisbursementService(
    GroupFinanceDbContext db,
    ILoanCalculationService calculationEngine,
    FinancialTransactionService financialTransactions)
{
    private static readonly string[] FundAccountTypes = ["Cash", "Bank", "Other"];

    public async Task<IReadOnlyCollection<LoanSummaryResponse>> ListLoansAsync(long groupId, string? status, long? memberId, CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var query = db.Loans.AsNoTracking()
            .Include(x => x.Member)
            .Where(x => x.GroupId == groupId);

        var normalizedStatus = status?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedStatus)) query = query.Where(x => x.Status == normalizedStatus);
        if (memberId.HasValue) query = query.Where(x => x.MemberId == memberId.Value);

        return await query.OrderByDescending(x => x.DisbursementDate).ThenByDescending(x => x.Id)
            .Take(500)
            .Select(x => new LoanSummaryResponse(
                x.Id, x.GroupId, x.MemberId, x.Member.MemberNumber, x.Member.FullName,
                x.LoanApplicationId ?? 0, x.LoanNumber, x.DisbursementDate, x.PrincipalAmount,
                x.InterestRate, x.InterestMethod, x.DurationMonths,
                x.Schedules.OrderBy(s => s.InstallmentNumber).Select(s => (decimal?)s.TotalDue).FirstOrDefault() ?? 0m,
                x.Schedules.Sum(s => s.InterestDue),
                x.Schedules.Sum(s => s.TotalDue),
                x.PrincipalPaid, x.InterestPaid, x.PenaltyPaid, x.OverpaymentAmount,
                x.PrincipalAmount - x.PrincipalPaid, x.Status))
            .ToArrayAsync(ct);
    }

    public async Task<LoanSummaryResponse?> GetLoanAsync(long id, CancellationToken ct)
    {
        var loan = await db.Loans.AsNoTracking().Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == id, ct);
        return loan is null ? null : await MapLoanAsync(loan, ct);
    }

    public async Task<IReadOnlyCollection<LoanScheduleResponse>?> GetScheduleAsync(long id, CancellationToken ct)
    {
        if (!await db.Loans.AnyAsync(x => x.Id == id, ct)) return null;
        return await db.LoanSchedules.AsNoTracking()
            .Where(x => x.LoanId == id)
            .OrderBy(x => x.InstallmentNumber)
            .Select(x => new LoanScheduleResponse(
                x.Id, x.LoanId, x.InstallmentNumber, x.DueDate, x.OpeningPrincipal,
                x.PrincipalDue, x.InterestDue, x.PenaltyDue, x.TotalDue,
                x.PrincipalPaid, x.InterestPaid, x.PenaltyPaid,
                x.TotalDue - x.PrincipalPaid - x.InterestPaid - x.PenaltyPaid, x.Status))
            .ToArrayAsync(ct);
    }

    public async Task<LoanDisbursementResponse> DisburseAsync(long applicationId, LoanDisbursementRequest request, long userId, CancellationToken ct)
    {
        if (request.FinancialAccountId <= 0) throw new LoanDisbursementValidationException("A disbursement financial account is required.");
        if (request.DisbursementDate == default) throw new LoanDisbursementValidationException("Disbursement date is required.");
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber) && request.ReferenceNumber.Trim().Length > 100)
            throw new LoanDisbursementValidationException("Reference number cannot exceed 100 characters.");
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 1000)
            throw new LoanDisbursementValidationException("Description cannot exceed 1000 characters.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var application = await db.LoanApplications
                .Include(x => x.Member).ThenInclude(x => x.Group)
                .Include(x => x.Guarantors)
                .SingleOrDefaultAsync(x => x.Id == applicationId, ct);

            if (application is null) throw new LoanDisbursementValidationException("Loan application not found.");
            if (application.Status != LoanApplicationStatuses.Approved)
                throw new LoanDisbursementValidationException("Only Approved loan applications can be disbursed.");
            if (application.Member.GroupId != application.Member.Group.Id || !application.Member.IsActive)
                throw new LoanDisbursementValidationException("The applicant must be an active member of the configured group at disbursement.");
            if (request.DisbursementDate.Date < application.ApplicationDate.Date)
                throw new LoanDisbursementValidationException("Disbursement date cannot be earlier than the loan application date.");

            var settings = await db.GroupSettings.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == application.Member.GroupId, ct)
                ?? throw new LoanDisbursementValidationException("The configured group does not have loan settings.");

            ValidatePostingDate(request.DisbursementDate, settings);

            if (await db.Loans.AnyAsync(x => x.LoanApplicationId == applicationId, ct))
                throw new LoanDisbursementValidationException("This loan application has already been disbursed.");

            var activeLoanCount = await db.Loans.CountAsync(x => x.MemberId == application.MemberId && x.Status != "Closed" && x.Status != "Cancelled", ct);
            if (activeLoanCount >= settings.MaximumActiveLoansPerMember)
                throw new LoanDisbursementValidationException("The member has already reached the maximum number of active loans.");

            // Take a snapshot because EF relationship fix-up can add newly-created
            // LoanGuarantor entities back into application.Guarantors while this
            // collection is being enumerated during the disbursement transaction.
            // Enumerating the live navigation collection would cause:
            // "Collection was modified; enumeration operation may not execute."
            var activeApplicationGuarantors = application.Guarantors
                .Where(x => x.Status == "Active")
                .ToList();
            var guarantorCount = activeApplicationGuarantors.Count;
            if (guarantorCount < settings.RequiredGuarantors)
                throw new LoanDisbursementValidationException($"At least {settings.RequiredGuarantors} active guarantor(s) are required before disbursement.");

            var sourceAccount = await db.FinancialAccounts.SingleOrDefaultAsync(
                x => x.Id == request.FinancialAccountId && x.GroupId == application.Member.GroupId && x.IsActive, ct)
                ?? throw new LoanDisbursementValidationException("The selected disbursement account does not exist, is inactive, or belongs to another group.");
            if (!FundAccountTypes.Contains(sourceAccount.AccountType, StringComparer.OrdinalIgnoreCase))
                throw new LoanDisbursementValidationException("The disbursement account must be a Cash, Bank, or Other fund account.");

            var sourceBalance = await GetAccountBalanceAsync(sourceAccount.Id, ct);
            if (sourceBalance < application.RequestedAmount)
                throw new LoanDisbursementValidationException($"Insufficient funds in the selected account. Available balance is {sourceBalance:N2}, but the loan requires {application.RequestedAmount:N2}.");

            var calculation = calculationEngine.Calculate(new LoanCalculationRequest(
                application.RequestedAmount,
                application.ProposedInterestRate,
                application.RequestedDurationMonths,
                application.ProposedInterestMethod,
                request.DisbursementDate.Date.AddMonths(1),
                settings.MonetaryPrecision));

            var now = DateTime.UtcNow;
            var loan = new Loan
            {
                GroupId = application.Member.GroupId,
                MemberId = application.MemberId,
                LoanApplicationId = application.Id,
                LoanNumber = $"LN-{Guid.NewGuid():N}"[..27],
                DisbursementDate = request.DisbursementDate.Date,
                PrincipalAmount = RoundMoney(application.RequestedAmount, settings.MonetaryPrecision),
                InterestRate = application.ProposedInterestRate,
                InterestMethod = application.ProposedInterestMethod,
                DurationMonths = application.RequestedDurationMonths,
                InstallmentFrequency = settings.LoanInstallmentFrequency,
                PrincipalPaid = 0m,
                InterestPaid = 0m,
                PenaltyPaid = 0m,
                OverpaymentAmount = 0m,
                Status = "Active",
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };
            db.Loans.Add(loan);
            await db.SaveChangesAsync(ct);

            // The Phase 10 guarantor rows are the source records for the loan guarantors.
            // Promote those existing rows to the newly-created loan instead of inserting
            // duplicate rows with the same (LoanApplicationId, MemberId). This preserves
            // the application-to-guarantor relationship and avoids violating the unique
            // application guarantor index.
            foreach (var applicationGuarantor in activeApplicationGuarantors)
            {
                applicationGuarantor.LoanId = loan.Id;
                applicationGuarantor.Status = "Active";
                applicationGuarantor.ModifiedAtUtc = now;
                applicationGuarantor.ModifiedByUserId = userId;
            }

            foreach (var line in calculation.Schedule)
            {
                db.LoanSchedules.Add(new LoanSchedule
                {
                    LoanId = loan.Id,
                    InstallmentNumber = line.InstallmentNumber,
                    DueDate = line.DueDate.Date,
                    OpeningPrincipal = line.OpeningPrincipal,
                    PrincipalDue = line.PrincipalDue,
                    InterestDue = line.InterestDue,
                    PenaltyDue = 0m,
                    TotalDue = line.TotalDue,
                    PrincipalPaid = 0m,
                    InterestPaid = 0m,
                    PenaltyPaid = 0m,
                    Status = "Due",
                    CreatedAtUtc = now,
                    CreatedByUserId = userId
                });
            }

            await db.SaveChangesAsync(ct);

            var loanReceivable = await EnsureLoanReceivableAccountAsync(application.Member.GroupId, userId, ct);
            var journal = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    application.Member.GroupId,
                    application.MemberId,
                    request.DisbursementDate.Date,
                    FinancialTransactionTypes.LoanDisbursement,
                    string.IsNullOrWhiteSpace(request.Description) ? $"Loan disbursement {loan.LoanNumber}" : request.Description.Trim(),
                    "LOAN_DISBURSEMENT",
                    loan.Id,
                    string.IsNullOrWhiteSpace(request.ReferenceNumber) ? loan.LoanNumber : request.ReferenceNumber.Trim(),
                    [
                        new FinancialTransactionLineRequest(loanReceivable.Id, application.RequestedAmount, 0m, "Member Loan Receivable"),
                        new FinancialTransactionLineRequest(sourceAccount.Id, 0m, application.RequestedAmount, "Loan disbursement from fund account")
                    ]), userId, ct);

            application.Status = LoanApplicationStatuses.Disbursed;
            application.ModifiedAtUtc = now;
            application.ModifiedByUserId = userId;
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return new LoanDisbursementResponse(
                loan.Id, application.Id, loan.LoanNumber, application.LoanApplicationNumber,
                loan.GroupId, application.Member.Group.Code, loan.MemberId, application.Member.MemberNumber,
                application.Member.FullName, loan.DisbursementDate, loan.PrincipalAmount, loan.InterestRate,
                loan.InterestMethod, loan.DurationMonths, loan.InstallmentFrequency,
                calculation.MonthlyInstallment, calculation.TotalInterest, calculation.TotalRepayment,
                loan.Status == "Active" ? "Active" : loan.Status, journal.Id, journal.TransactionNumber,
                sourceAccount.Id, sourceAccount.AccountCode, sourceAccount.AccountName, calculation.Schedule.Count);
        });
    }

    private async Task<LoanSummaryResponse> MapLoanAsync(Loan loan, CancellationToken ct)
    {
        var totals = await db.LoanSchedules.AsNoTracking().Where(x => x.LoanId == loan.Id)
            .GroupBy(_ => 1)
            .Select(g => new { Installment = g.OrderBy(x => x.InstallmentNumber).Select(x => x.TotalDue).FirstOrDefault(), Interest = g.Sum(x => x.InterestDue), Total = g.Sum(x => x.TotalDue) })
            .SingleOrDefaultAsync(ct);
        return new LoanSummaryResponse(
            loan.Id, loan.GroupId, loan.MemberId, loan.Member.MemberNumber, loan.Member.FullName,
            loan.LoanApplicationId ?? 0, loan.LoanNumber, loan.DisbursementDate, loan.PrincipalAmount,
            loan.InterestRate, loan.InterestMethod, loan.DurationMonths, totals?.Installment ?? 0m,
            totals?.Interest ?? 0m, totals?.Total ?? 0m, loan.PrincipalPaid, loan.InterestPaid,
            loan.PenaltyPaid, loan.OverpaymentAmount, loan.PrincipalAmount - loan.PrincipalPaid, loan.Status);
    }

    private async Task<FinancialAccount> EnsureLoanReceivableAccountAsync(long groupId, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == "LOAN-RECEIVABLE", ct);
        if (account is not null)
        {
            if (!account.IsActive || !string.Equals(account.AccountType, "Asset", StringComparison.OrdinalIgnoreCase))
                throw new LoanDisbursementValidationException("The system Loan Receivable account is inactive or has an invalid account type.");
            return account;
        }

        account = new FinancialAccount
        {
            GroupId = groupId,
            AccountCode = "LOAN-RECEIVABLE",
            AccountName = "Member Loan Receivable",
            AccountType = "Asset",
            IsSystemAccount = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<decimal> GetAccountBalanceAsync(long accountId, CancellationToken ct) =>
        await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted")
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;

    private static void ValidatePostingDate(DateTime date, GroupSetting settings)
    {
        var day = date.Date;
        var today = DateTime.UtcNow.Date;
        if (day < today && !settings.AllowBackdatedTransactions)
            throw new LoanDisbursementValidationException("Backdated financial transactions are disabled for this group.");
        if (day > today && !settings.AllowFutureFinancialPosting)
            throw new LoanDisbursementValidationException("Future financial posting is disabled for this group.");
    }

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (groupId <= 0 || !await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new LoanDisbursementValidationException("The selected group does not exist or is inactive.");
    }

    private static decimal RoundMoney(decimal value, int precision) => Math.Round(value, precision, MidpointRounding.AwayFromZero);
}
