using GroupFinance.Core.InterestProfit;
using GroupFinance.Infrastructure.Persistence;
using NepDate;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Dashboard;

public sealed class DashboardValidationException(string message) : Exception(message);

public sealed class DashboardService(GroupFinanceDbContext db)
{
    public async Task<DashboardResponse> GetAsync(long groupId, DateTime? asOfDate, CancellationToken ct)
    {
        if (groupId <= 0)
            throw new DashboardValidationException("GroupId is required.");

        var date = (asOfDate ?? DateTime.Today).Date;
        var endExclusive = date.AddDays(1);

        var group = await db.Groups.AsNoTracking()
            .Include(x => x.Setting)
            .SingleOrDefaultAsync(x => x.Id == groupId && x.IsActive, ct)
            ?? throw new DashboardValidationException("The selected group does not exist or is inactive.");

        var configuredGroupCount = await db.Groups.CountAsync(x => x.IsActive, ct);
        if (configuredGroupCount != 1)
            throw new DashboardValidationException("The system requires exactly one active configured group.");

        var precision = Math.Clamp(group.Setting?.MonetaryPrecision ?? 2, 0, 6);

        var totalMembers = await db.Members.AsNoTracking()
            .CountAsync(x => x.GroupId == groupId, ct);

        var activeMembers = await db.Members.AsNoTracking()
            .CountAsync(x => x.GroupId == groupId && x.IsActive && x.JoinDate.Date <= date &&
                (x.ExitDate == null || x.ExitDate.Value.Date > date), ct);

        var totalContributions = await db.ContributionPayments.AsNoTracking()
            .Where(x => x.Member.GroupId == groupId &&
                        x.Status == "Posted" &&
                        x.TransactionDate < endExclusive)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var loans = await db.Loans.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.DisbursementDate < endExclusive)
            .Select(x => new
            {
                x.Id,
                x.PrincipalAmount,
                x.PrincipalPaid,
                x.Status
            })
            .ToListAsync(ct);

        var totalLoans = loans.Sum(x => x.PrincipalAmount);
        var outstandingLoans = loans.Sum(x => Math.Max(0m, x.PrincipalAmount - x.PrincipalPaid));

        var postedLines = await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == groupId &&
                        x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < endExclusive)
            .Select(x => new DashboardLine(
                x.FinancialTransaction.TransactionDate,
                x.FinancialAccount.AccountCode,
                x.FinancialAccount.AccountType,
                x.FinancialTransaction.TransactionType,
                x.DebitAmount,
                x.CreditAmount))
            .ToListAsync(ct);

        var profitLines = postedLines
            .Select(x => new ProfitLineInput(
                x.AccountCode,
                x.AccountType,
                x.TransactionType,
                x.DebitAmount,
                x.CreditAmount))
            .ToArray();

        var profit = ProfitCalculationEngine.Calculate(
            profitLines,
            group.Setting?.ProfitReserveRate ?? 10m,
            precision);

        var interestEarned = profit.LoanInterestIncome;
        var expenses = profit.Expenses;

        var fundAccounts = await db.FinancialAccounts.AsNoTracking()
            .Where(x => x.GroupId == groupId &&
                        x.IsActive &&
                        (x.AccountType == "Cash" || x.AccountType == "Bank" || x.AccountType == "Other"))
            .Select(x => new { x.Id, x.AccountType })
            .ToListAsync(ct);

        var fundAccountIds = fundAccounts.Select(x => x.Id).ToArray();

        var fundBalances = fundAccountIds.Length == 0
            ? []
            : await db.TransactionDetails.AsNoTracking()
                .Where(x => fundAccountIds.Contains(x.FinancialAccountId) &&
                            x.FinancialTransaction.Status == "Posted" &&
                            x.FinancialTransaction.TransactionDate < endExclusive)
                .GroupBy(x => x.FinancialAccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Balance = g.Sum(x => x.DebitAmount - x.CreditAmount)
                })
                .ToListAsync(ct);

        var availableCash = fundBalances.Sum(x => x.Balance);

        var assets = await db.Assets.AsNoTracking()
            .Where(x => x.GroupId == groupId &&
                        x.IsActive &&
                        x.AcquisitionDate < endExclusive)
            .Select(x => new { x.Category, x.BookValue })
            .ToListAsync(ct);

        var investmentValue = assets
            .Where(x => x.Category.Equals("Investment", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.BookValue);

        var otherAssetValue = assets
            .Where(x => !x.Category.Equals("Investment", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.BookValue);

        // Keep the database predicate SQL-translatable. EF Core/SQL Server does not
        // translate Math.Max(decimal, decimal) in this query shape. Load only the
        // overdue schedule rows and perform the non-negative outstanding calculation
        // in memory. This is also consistent with the overdue-loan module.
        var overdueSchedules = await db.LoanSchedules.AsNoTracking()
            .Where(x => x.Loan.GroupId == groupId &&
                        x.Loan.Status == "Active" &&
                        x.DueDate < date)
            .Select(x => new
            {
                x.LoanId,
                x.PrincipalDue,
                x.PrincipalPaid,
                x.InterestDue,
                x.InterestPaid,
                x.PenaltyDue,
                x.PenaltyPaid
            })
            .ToListAsync(ct);

        var overdueAmounts = overdueSchedules
            .Select(x => new
            {
                x.LoanId,
                Outstanding = Math.Max(0m, x.PrincipalDue - x.PrincipalPaid) +
                              Math.Max(0m, x.InterestDue - x.InterestPaid) +
                              Math.Max(0m, x.PenaltyDue - x.PenaltyPaid)
            })
            .Where(x => x.Outstanding > 0m)
            .ToArray();

        var overdueLoans = overdueAmounts.Select(x => x.LoanId).Distinct().Count();
        var overdueAmount = overdueAmounts.Sum(x => x.Outstanding);

        var monthlyCollections = await BuildMonthlyCollectionsAsync(groupId, date, ct);
        var loanStatus = new[]
        {
            new DashboardLoanStatusResponse("Active", loans.Count(x => x.Status == "Active")),
            new DashboardLoanStatusResponse("Closed", loans.Count(x => x.Status == "Closed")),
            new DashboardLoanStatusResponse("Overdue", overdueLoans)
        };

        var incomeVsExpense = BuildIncomeExpense(postedLines, date, group.Setting?.ProfitReserveRate ?? 10m, precision);

        return new DashboardResponse(
            groupId,
            date,
            group.CurrencyCode,
            totalMembers,
            activeMembers,
            Round(totalContributions, precision),
            Round(totalLoans, precision),
            Round(outstandingLoans, precision),
            Round(interestEarned, precision),
            Round(expenses, precision),
            Round(profit.NetProfit, precision),
            Round(availableCash, precision),
            overdueLoans,
            Round(overdueAmount, precision),
            new DashboardFundDistributionResponse(
                Round(availableCash, precision),
                Round(outstandingLoans, precision),
                Round(otherAssetValue, precision),
                Round(investmentValue, precision)),
            monthlyCollections,
            loanStatus,
            incomeVsExpense);
    }

    private async Task<IReadOnlyCollection<DashboardMonthlyCollectionResponse>> BuildMonthlyCollectionsAsync(
        long groupId,
        DateTime asOfDate,
        CancellationToken ct)
    {
        var asOfBs = new NepaliDate(asOfDate);
        var bsYearStart = FindBsYearStart(asOfDate);
        var bsNextYearStart = FindNextBsYearStart(bsYearStart);
        var endExclusive = asOfDate.AddDays(1);

        var rows = await db.ContributionPayments.AsNoTracking()
            .Where(x => x.Member.GroupId == groupId &&
                        x.Status == "Posted" &&
                        x.TransactionDate >= bsYearStart &&
                        x.TransactionDate < endExclusive &&
                        x.TransactionDate < bsNextYearStart)
            .Select(x => new { x.TransactionDate, x.Amount })
            .ToListAsync(ct);

        var amounts = rows
            .Select(x => new { Bs = new NepaliDate(x.TransactionDate), x.Amount })
            .Where(x => x.Bs.Year == asOfBs.Year)
            .GroupBy(x => x.Bs.Month)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var monthNames = GetBsMonthNames(bsYearStart, bsNextYearStart, asOfBs.Year);

        return Enumerable.Range(1, 12)
            .Select(month => new DashboardMonthlyCollectionResponse(
                month,
                monthNames.TryGetValue(month, out var name) ? name : $"Month {month}",
                Round(amounts.TryGetValue(month, out var amount) ? amount : 0m, 2)))
            .ToArray();
    }

    private static DateTime FindBsYearStart(DateTime date)
    {
        var current = date.Date;
        var year = new NepaliDate(current).Year;
        for (var i = 0; i < 370; i++)
        {
            var previous = current.AddDays(-1);
            if (new NepaliDate(previous).Year != year)
                return current;
            current = previous;
        }

        throw new InvalidOperationException("Unable to determine the start of the current Nepali year.");
    }

    private static DateTime FindNextBsYearStart(DateTime bsYearStart)
    {
        var year = new NepaliDate(bsYearStart).Year;
        var current = bsYearStart.AddDays(1);
        for (var i = 0; i < 370; i++)
        {
            if (new NepaliDate(current).Year != year)
                return current;
            current = current.AddDays(1);
        }

        throw new InvalidOperationException("Unable to determine the end of the current Nepali year.");
    }

    private static Dictionary<int, string> GetBsMonthNames(
        DateTime bsYearStart,
        DateTime bsNextYearStart,
        int bsYear)
    {
        var result = new Dictionary<int, string>();
        var current = bsYearStart;

        while (current < bsNextYearStart && result.Count < 12)
        {
            var bs = new NepaliDate(current);
            if (bs.Year == bsYear && !result.ContainsKey(bs.Month))
                result[bs.Month] = bs.MonthName.ToString();

            current = current.AddDays(1);
        }

        return result;
    }

    private static IReadOnlyCollection<DashboardIncomeExpenseResponse> BuildIncomeExpense(
        IReadOnlyCollection<DashboardLine> lines,
        DateTime asOfDate,
        decimal reserveRate,
        int precision)
    {
        var asOfBs = new NepaliDate(asOfDate);
        var bsYearStart = FindBsYearStart(asOfDate);
        var bsNextYearStart = FindNextBsYearStart(bsYearStart);
        var monthNames = GetBsMonthNames(bsYearStart, bsNextYearStart, asOfBs.Year);

        return Enumerable.Range(1, 12)
            .Select(month =>
            {
                var monthLines = lines
                    .Where(x =>
                    {
                        var bs = new NepaliDate(x.TransactionDate);
                        return bs.Year == asOfBs.Year &&
                               bs.Month == month &&
                               x.TransactionDate <= asOfDate;
                    })
                    .Select(x => new ProfitLineInput(
                        x.AccountCode,
                        x.AccountType,
                        x.TransactionType,
                        x.DebitAmount,
                        x.CreditAmount))
                    .ToArray();

                var result = ProfitCalculationEngine.Calculate(monthLines, reserveRate, precision);
                var name = monthNames.TryGetValue(month, out var monthName)
                    ? monthName
                    : $"Month {month}";

                return new DashboardIncomeExpenseResponse(
                    month,
                    name,
                    Round(result.TotalIncome, precision),
                    Round(result.TotalCosts, precision),
                    Round(result.NetProfit, precision));
            })
            .ToArray();
    }

    private sealed record DashboardLine(
        DateTime TransactionDate,
        string AccountCode,
        string AccountType,
        string TransactionType,
        decimal DebitAmount,
        decimal CreditAmount);

    private static decimal Round(decimal value, int precision) =>
        decimal.Round(value, precision, MidpointRounding.AwayFromZero);
}
