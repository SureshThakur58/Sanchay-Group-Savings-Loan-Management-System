using GroupFinance.Core.Entities;
using GroupFinance.Core.InterestProfit;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.InterestProfit;

public sealed class InterestProfitValidationException(string message) : Exception(message);

public sealed class InterestProfitService(GroupFinanceDbContext db)
{
    public async Task<IReadOnlyCollection<InterestCalculationResponse>> CalculateInterestAsync(
        CalculateInterestRequest request,
        long userId,
        CancellationToken ct)
    {
        ValidatePeriod(request.GroupId, request.PeriodStart, request.PeriodEnd);
        var group = await db.Groups.Include(x => x.Setting)
            .SingleOrDefaultAsync(x => x.Id == request.GroupId && x.IsActive, ct)
            ?? throw new InterestProfitValidationException("The selected group does not exist or is inactive.");
        var precision = group.Setting?.MonetaryPrecision ?? 2;
        var start = request.PeriodStart.Date;
        var end = request.PeriodEnd.Date;

        var loansQuery = db.Loans.AsNoTracking()
            .Include(x => x.Member)
            .Where(x => x.GroupId == request.GroupId && x.DisbursementDate.Date <= end);
        if (request.LoanId.HasValue)
            loansQuery = loansQuery.Where(x => x.Id == request.LoanId.Value);

        var loans = await loansQuery
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        if (request.LoanId.HasValue && loans.Count == 0)
            throw new InterestProfitValidationException("Loan not found in the selected group.");

        var loanIds = loans.Select(x => x.Id).ToArray();
        if (loanIds.Length == 0)
            return [];

        var schedules = await db.LoanSchedules.AsNoTracking()
            .Where(x => loanIds.Contains(x.LoanId) && x.DueDate >= start && x.DueDate < end.AddDays(1))
            .OrderBy(x => x.LoanId).ThenBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber)
            .ToListAsync(ct);

        var principalPaidBefore = await db.LoanPayments.AsNoTracking()
            .Where(x => loanIds.Contains(x.LoanId) && x.Status == "Posted" && x.TransactionDate < start)
            .GroupBy(x => x.LoanId)
            .Select(g => new { LoanId = g.Key, PrincipalPaid = g.Sum(x => x.PrincipalAllocated) })
            .ToDictionaryAsync(x => x.LoanId, x => x.PrincipalPaid, ct);

        var results = new List<InterestCalculationResponse>();
        var now = DateTime.UtcNow;

        foreach (var loan in loans)
        {
            var loanSchedules = schedules.Where(x => x.LoanId == loan.Id).ToArray();
            if (loanSchedules.Length == 0) continue;

            var openingPrincipal = loan.DisbursementDate.Date >= start
                ? 0m
                : Math.Max(0m, loan.PrincipalAmount - principalPaidBefore.GetValueOrDefault(loan.Id));

            var calculation = InterestCalculationEngine.Calculate(
                openingPrincipal,
                loanSchedules.Select(x => new InterestScheduleInput(x.Id, x.DueDate, x.OpeningPrincipal, x.InterestDue)),
                start,
                end,
                precision);

            var existing = await db.InterestCalculations
                .SingleOrDefaultAsync(x => x.LoanId == loan.Id && x.PeriodStart == start && x.PeriodEnd == end, ct);

            if (existing is null)
            {
                existing = new InterestCalculation
                {
                    LoanId = loan.Id,
                    CalculationDate = now,
                    PeriodStart = start,
                    PeriodEnd = end,
                    OpeningPrincipal = calculation.OpeningPrincipal,
                    InterestRate = loan.InterestRate,
                    InterestAmount = calculation.InterestAmount,
                    CalculationMethod = loan.InterestMethod,
                    IsPosted = false,
                    CreatedAtUtc = now,
                    CreatedByUserId = userId
                };
                db.InterestCalculations.Add(existing);
            }
            else if (!existing.IsPosted)
            {
                existing.CalculationDate = now;
                existing.OpeningPrincipal = calculation.OpeningPrincipal;
                existing.InterestRate = loan.InterestRate;
                existing.InterestAmount = calculation.InterestAmount;
                existing.CalculationMethod = loan.InterestMethod;
                existing.ModifiedAtUtc = now;
                existing.ModifiedByUserId = userId;
            }

            await db.SaveChangesAsync(ct);

            results.Add(MapInterest(existing, loan));
        }

        return results;
    }

    public async Task<IReadOnlyCollection<InterestCalculationResponse>> ListInterestAsync(
        long groupId,
        DateTime? fromDate,
        DateTime? toDate,
        long? loanId,
        CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var query = db.InterestCalculations.AsNoTracking()
            .Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId);
        if (fromDate.HasValue) query = query.Where(x => x.PeriodEnd >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.PeriodStart <= toDate.Value.Date);
        if (loanId.HasValue) query = query.Where(x => x.LoanId == loanId.Value);

        var rows = await query.OrderByDescending(x => x.PeriodStart).ThenBy(x => x.LoanId).Take(1000).ToListAsync(ct);
        return rows.Select(x => MapInterest(x, x.Loan)).ToArray();
    }

    public async Task<ProfitCalculationResponse> CalculateProfitAsync(
        ProfitCalculationRequest request,
        CancellationToken ct)
    {
        ValidatePeriod(request.GroupId, request.PeriodStart, request.PeriodEnd);
        var group = await db.Groups.Include(x => x.Setting)
            .SingleOrDefaultAsync(x => x.Id == request.GroupId && x.IsActive, ct)
            ?? throw new InterestProfitValidationException("The selected group does not exist or is inactive.");

        var start = request.PeriodStart.Date;
        var endExclusive = request.PeriodEnd.Date.AddDays(1);
        var precision = group.Setting?.MonetaryPrecision ?? 2;
        var reserveRate = group.Setting?.ProfitReserveRate ?? 10m;

        var lines = await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == request.GroupId
                && x.FinancialTransaction.Status == "Posted"
                && x.FinancialTransaction.TransactionDate >= start
                && x.FinancialTransaction.TransactionDate < endExclusive)
            .Select(x => new ProfitLineInput(
                x.FinancialAccount.AccountCode,
                x.FinancialAccount.AccountType,
                x.FinancialTransaction.TransactionType,
                x.DebitAmount,
                x.CreditAmount))
            .ToListAsync(ct);

        var result = ProfitCalculationEngine.Calculate(lines, reserveRate, precision);
        return new ProfitCalculationResponse(
            request.GroupId,
            start,
            request.PeriodEnd.Date,
            result.LoanInterestIncome,
            result.InvestmentIncome,
            result.OtherIncome,
            result.TotalIncome,
            result.Expenses,
            result.OtherCosts,
            result.TotalCosts,
            result.NetProfit,
            reserveRate,
            result.ReserveAmount,
            result.DistributableAmount);
    }

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (!await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new InterestProfitValidationException("The selected group does not exist or is inactive.");
    }

    private static void ValidatePeriod(long groupId, DateTime start, DateTime end)
    {
        if (groupId <= 0) throw new InterestProfitValidationException("GroupId is required.");
        if (start == default) throw new InterestProfitValidationException("Period start is required.");
        if (end == default) throw new InterestProfitValidationException("Period end is required.");
        if (end.Date < start.Date) throw new InterestProfitValidationException("Period end cannot be earlier than period start.");
    }

    private static InterestCalculationResponse MapInterest(InterestCalculation x, Loan loan) =>
        new(x.Id, x.LoanId, loan.LoanNumber, loan.MemberId, loan.Member.FullName,
            x.CalculationDate, x.PeriodStart, x.PeriodEnd, x.OpeningPrincipal,
            x.InterestRate, x.InterestAmount, x.CalculationMethod, x.IsPosted);
}
