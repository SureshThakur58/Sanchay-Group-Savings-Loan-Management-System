using GroupFinance.Core.InterestProfit;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Reports;

public sealed class ReportService(GroupFinanceDbContext db)
{
    public async Task<ReportSummaryResponse> GetSummaryAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        var context = await GetContextAsync(groupId, asOfDate, ct);
        var start = new DateTime(asOfDate.Year, 1, 1);
        var end = asOfDate.Date.AddDays(1);

        var totalMembers = await db.Members.CountAsync(x => x.GroupId == groupId, ct);
        var activeMembers = await db.Members.CountAsync(x => x.GroupId == groupId && x.IsActive &&
            x.JoinDate < end && (x.ExitDate == null || x.ExitDate > asOfDate.Date), ct);

        var contributions = await db.ContributionPayments.AsNoTracking()
            .Where(x => x.Member.GroupId == groupId && x.Status == "Posted" && x.TransactionDate < end)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var loans = await db.Loans.AsNoTracking().Where(x => x.GroupId == groupId && x.DisbursementDate < end)
            .Select(x => new { x.PrincipalAmount, x.PrincipalPaid }).ToListAsync(ct);

        var postedLines = await GetPostedLinesAsync(groupId, end, ct);
        var profit = ProfitCalculationEngine.Calculate(
            postedLines.Select(x => new ProfitLineInput(x.Code, x.Type, x.TransactionType, x.Debit, x.Credit)).ToArray(),
            context.ReserveRate, context.Precision);

        var fund = await GetFundBalanceAsync(groupId, end, ct);
        var overdue = await GetOverdueRowsAsync(groupId, asOfDate.Date, ct);

        return new(
            groupId, context.GroupCode, context.GroupName, asOfDate.Date, context.CurrencyCode,
            totalMembers, activeMembers, Round(contributions, context.Precision),
            Round(loans.Sum(x => x.PrincipalAmount), context.Precision),
            Round(loans.Sum(x => Math.Max(0m, x.PrincipalAmount - x.PrincipalPaid)), context.Precision),
            Round(profit.LoanInterestIncome, context.Precision),
            Round(profit.Expenses, context.Precision), Round(profit.NetProfit, context.Precision),
            Round(fund, context.Precision), overdue.Select(x => x.LoanId).Distinct().Count(),
            Round(overdue.Sum(x => x.Outstanding), context.Precision));
    }

    public async Task<IReadOnlyCollection<MemberReportResponse>> GetMembersAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, asOfDate, ct);
        var end = asOfDate.Date.AddDays(1);
        var members = await db.Members.AsNoTracking().Where(x => x.GroupId == groupId).OrderBy(x => x.MemberNumber).ToListAsync(ct);
        var memberIds = members.Select(x => x.Id).ToArray();

        var contributions = await db.ContributionPayments.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.Status == "Posted" && x.TransactionDate < end)
            .GroupBy(x => x.MemberId).Select(g => new { MemberId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Amount, ct);

        var loans = await db.Loans.AsNoTracking().Where(x => memberIds.Contains(x.MemberId) && x.DisbursementDate < end)
            .GroupBy(x => x.MemberId).Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.PrincipalAmount), Outstanding = g.Sum(x => x.PrincipalAmount - x.PrincipalPaid) })
            .ToDictionaryAsync(x => x.MemberId, ct);

        return members.Select(x =>
        {
            loans.TryGetValue(x.Id, out var loan);
            contributions.TryGetValue(x.Id, out var contribution);
            return new MemberReportResponse(x.Id, x.MemberNumber, x.FullName, x.MobileNumber, x.Email,
                x.JoinDate, x.ExitDate, x.IsActive, contribution, loan?.Total ?? 0m, Math.Max(0m, loan?.Outstanding ?? 0m));
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<ContributionReportResponse>> GetContributionsAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? memberId, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var query = db.ContributionPayments.AsNoTracking().Include(x => x.Member)
            .Where(x => x.Member.GroupId == groupId && x.Status == "Posted");
        if (fromDate.HasValue) query = query.Where(x => x.TransactionDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.TransactionDate < toDate.Value.Date.AddDays(1));
        if (memberId.HasValue) query = query.Where(x => x.MemberId == memberId.Value);
        var rows = await query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id).Take(5000).ToListAsync(ct);
        return rows.Select(x => new ContributionReportResponse(x.Id, x.MemberId, x.Member.MemberNumber, x.Member.FullName,
            x.TransactionDate, x.Amount, x.PaymentMethod, x.ReferenceNumber, x.Description, x.Status)).ToArray();
    }

    public async Task<IReadOnlyCollection<CollectionDueReportResponse>> GetCollectionDueAsync(long groupId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate, ct);
        ValidateRange(fromDate, toDate);
        var rows = await db.ContributionSchedules.AsNoTracking().Include(x => x.Member)
            .Where(x => x.Member.GroupId == groupId && x.DueDate >= fromDate.Date && x.DueDate < toDate.Date.AddDays(1))
            .OrderBy(x => x.DueDate).ThenBy(x => x.Member.MemberNumber).Take(5000).ToListAsync(ct);
        return rows.Select(MapCollection).Where(x => x.OutstandingAmount > 0m).ToArray();
    }

    public async Task<IReadOnlyCollection<CollectionDueReportResponse>> GetCollectionOutstandingAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, asOfDate, ct);
        var rows = await db.ContributionSchedules.AsNoTracking().Include(x => x.Member)
            .Where(x => x.Member.GroupId == groupId && x.DueDate < asOfDate.Date.AddDays(1))
            .OrderBy(x => x.DueDate).ThenBy(x => x.Member.MemberNumber).Take(5000).ToListAsync(ct);
        return rows.Select(MapCollection).Where(x => x.OutstandingAmount > 0m).ToArray();
    }

    public async Task<IReadOnlyCollection<LoanRegisterReportResponse>> GetLoanRegisterAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, asOfDate, ct);
        var end = asOfDate.Date.AddDays(1);
        var rows = await db.Loans.AsNoTracking().Include(x => x.Member)
            .Where(x => x.GroupId == groupId && x.DisbursementDate < end)
            .OrderByDescending(x => x.DisbursementDate).ThenByDescending(x => x.Id).Take(5000).ToListAsync(ct);
        return rows.Select(x => new LoanRegisterReportResponse(x.Id, x.LoanNumber, x.MemberId, x.Member.MemberNumber, x.Member.FullName,
            x.DisbursementDate, x.PrincipalAmount, x.InterestRate, x.InterestMethod, x.DurationMonths,
            x.PrincipalPaid, x.InterestPaid, x.PenaltyPaid, Math.Max(0m, x.PrincipalAmount - x.PrincipalPaid), x.Status)).ToArray();
    }

    public async Task<IReadOnlyCollection<LoanRegisterReportResponse>> GetLoanOutstandingAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        return (await GetLoanRegisterAsync(groupId, asOfDate, ct)).Where(x => x.OutstandingPrincipal > 0m).ToArray();
    }

    public async Task<IReadOnlyCollection<LoanRepaymentReportResponse>> GetLoanRepaymentsAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? memberId, long? loanId, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var query = db.LoanPayments.AsNoTracking().Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId && x.Status == "Posted");
        if (fromDate.HasValue) query = query.Where(x => x.TransactionDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.TransactionDate < toDate.Value.Date.AddDays(1));
        if (memberId.HasValue) query = query.Where(x => x.Loan.MemberId == memberId.Value);
        if (loanId.HasValue) query = query.Where(x => x.LoanId == loanId.Value);
        var rows = await query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id).Take(5000).ToListAsync(ct);
        return rows.Select(x => new LoanRepaymentReportResponse(x.Id, x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId,
            x.Loan.Member.MemberNumber, x.Loan.Member.FullName, x.TransactionDate, x.Amount, x.PrincipalAllocated,
            x.InterestAllocated, x.PenaltyAllocated, x.OverpaymentAmount, x.PaymentMethod, x.ReferenceNumber, x.Description, x.Status)).ToArray();
    }

    public async Task<IReadOnlyCollection<LoanInstallmentReportResponse>> GetLoanInstallmentsAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? loanId, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var query = db.LoanSchedules.AsNoTracking().Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId);
        if (fromDate.HasValue) query = query.Where(x => x.DueDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.DueDate < toDate.Value.Date.AddDays(1));
        if (loanId.HasValue) query = query.Where(x => x.LoanId == loanId.Value);
        var rows = await query.OrderBy(x => x.LoanId).ThenBy(x => x.InstallmentNumber).Take(10000).ToListAsync(ct);
        return rows.Select(x => new LoanInstallmentReportResponse(x.Id, x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId,
            x.Loan.Member.MemberNumber, x.Loan.Member.FullName, x.InstallmentNumber, x.DueDate, x.OpeningPrincipal,
            x.PrincipalDue, x.InterestDue, x.PenaltyDue, x.TotalDue, x.PrincipalPaid, x.InterestPaid,
            x.PenaltyPaid, Math.Max(0m, x.PrincipalDue - x.PrincipalPaid) + Math.Max(0m, x.InterestDue - x.InterestPaid) +
            Math.Max(0m, x.PenaltyDue - x.PenaltyPaid), x.Status)).ToArray();
    }

    public async Task<IReadOnlyCollection<OverdueReportResponse>> GetOverdueAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, asOfDate, ct);
        var rows = await db.LoanSchedules.AsNoTracking().Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId && x.Loan.Status == "Active" && x.DueDate < asOfDate.Date)
            .OrderBy(x => x.DueDate).ThenBy(x => x.LoanId).ToListAsync(ct);
        return rows.Select(x =>
        {
            var principal = Math.Max(0m, x.PrincipalDue - x.PrincipalPaid);
            var interest = Math.Max(0m, x.InterestDue - x.InterestPaid);
            var penalty = Math.Max(0m, x.PenaltyDue - x.PenaltyPaid);
            return new OverdueReportResponse(x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId, x.Loan.Member.MemberNumber,
                x.Loan.Member.FullName, x.InstallmentNumber, x.DueDate, Math.Max(0, (asOfDate.Date - x.DueDate.Date).Days),
                principal, interest, penalty, principal + interest + penalty);
        }).Where(x => x.OutstandingAmount > 0m).ToArray();
    }

    public async Task<IReadOnlyCollection<InterestIncomeReportResponse>> GetInterestIncomeAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? loanId, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var query = db.InterestCalculations.AsNoTracking().Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId);
        if (fromDate.HasValue) query = query.Where(x => x.PeriodEnd >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.PeriodStart <= toDate.Value.Date);
        if (loanId.HasValue) query = query.Where(x => x.LoanId == loanId.Value);
        var rows = await query.OrderByDescending(x => x.PeriodStart).ThenBy(x => x.LoanId).Take(5000).ToListAsync(ct);
        return rows.Select(x => new InterestIncomeReportResponse(x.Id, x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId,
            x.Loan.Member.FullName, x.PeriodStart, x.PeriodEnd, x.OpeningPrincipal, x.InterestRate,
            x.InterestAmount, x.CalculationMethod, x.IsPosted)).ToArray();
    }

    public async Task<IReadOnlyCollection<ProfitDistributionReportResponse>> GetProfitDistributionAsync(long groupId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var query = db.ProfitDistributionDetails.AsNoTracking()
            .Include(x => x.Member).Include(x => x.ProfitDistribution)
            .Where(x => x.ProfitDistribution.GroupId == groupId);
        if (fromDate.HasValue) query = query.Where(x => x.ProfitDistribution.PeriodEnd >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.ProfitDistribution.PeriodStart <= toDate.Value.Date);
        var rows = await query.OrderByDescending(x => x.ProfitDistribution.PeriodEnd).ThenBy(x => x.Member.MemberNumber).Take(5000).ToListAsync(ct);
        return rows.Select(x => new ProfitDistributionReportResponse(x.ProfitDistributionId, x.ProfitDistribution.PeriodStart,
            x.ProfitDistribution.PeriodEnd, x.ProfitDistribution.NetProfit, x.ProfitDistribution.ReserveAmount,
            x.ProfitDistribution.DistributableAmount, x.ProfitDistribution.DistributionMethod,
            x.ProfitDistribution.Status, x.ProfitDistribution.DistributedDate, x.MemberId, x.Member.MemberNumber,
            x.Member.FullName, x.DistributedAmount)).ToArray();
    }

    public async Task<IReadOnlyCollection<ExpenseReportResponse>> GetExpensesAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? categoryId, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var query = db.Expenses.AsNoTracking().Include(x => x.ExpenseCategory)
            .Include(x => x.FinancialTransaction).ThenInclude(x => x!.Details).ThenInclude(x => x.FinancialAccount)
            .Where(x => x.GroupId == groupId && x.Status == "Posted");
        if (fromDate.HasValue) query = query.Where(x => x.ExpenseDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.ExpenseDate < toDate.Value.Date.AddDays(1));
        if (categoryId.HasValue) query = query.Where(x => x.ExpenseCategoryId == categoryId.Value);
        var rows = await query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Id).Take(5000).ToListAsync(ct);
        return rows.Select(x =>
        {
            var account = x.FinancialTransaction?.Details.FirstOrDefault(d => d.DebitAmount > 0)?.FinancialAccount;
            return new ExpenseReportResponse(x.Id, x.ExpenseNumber, x.ExpenseDate, x.ExpenseCategoryId, x.ExpenseCategory.Name,
                x.Amount, x.PaymentMethod, account?.Id ?? 0, account?.AccountCode ?? "-", account?.AccountName ?? "-",
                x.ReferenceNumber, x.Description, x.Status);
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<AssetReportResponse>> GetAssetsAsync(long groupId, DateTime asOfDate, bool includeInactive, CancellationToken ct)
    {
        await GetContextAsync(groupId, asOfDate, ct);
        var end = asOfDate.Date.AddDays(1);
        var query = db.Assets.AsNoTracking().Where(x => x.GroupId == groupId && x.AcquisitionDate < end);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var rows = await query.OrderBy(x => x.AssetCode).Take(5000).ToListAsync(ct);
        return rows.Select(x => new AssetReportResponse(x.Id, x.AssetCode, x.AssetName, x.Category, x.AcquisitionDate,
            x.AcquisitionCost, x.BookValue, x.Location, x.Description, x.IsActive, x.IsActive ? "Active" : "Sold")).ToArray();
    }

    public async Task<IReadOnlyCollection<CashBankReportResponse>> GetCashBankAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, asOfDate, ct);
        var end = asOfDate.Date.AddDays(1);
        var accounts = await db.FinancialAccounts.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.IsActive && (x.AccountType == "Cash" || x.AccountType == "Bank"))
            .OrderBy(x => x.AccountCode).ToListAsync(ct);
        var ids = accounts.Select(x => x.Id).ToArray();
        var totals = ids.Length == 0 ? [] : await db.TransactionDetails.AsNoTracking()
            .Where(x => ids.Contains(x.FinancialAccountId) && x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < end)
            .GroupBy(x => x.FinancialAccountId)
            .Select(g => new { Id = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);
        return accounts.Select(x =>
        {
            var total = totals.FirstOrDefault(t => t.Id == x.Id);
            var debit = total?.Debit ?? 0m; var credit = total?.Credit ?? 0m;
            return new CashBankReportResponse(x.Id, x.AccountCode, x.AccountName, x.AccountType, debit, credit, debit - credit);
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<IncomeExpenseReportResponse>> GetIncomeExpenseAsync(long groupId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        await GetContextAsync(groupId, toDate, ct);
        ValidateRange(fromDate, toDate);
        var end = toDate.Date.AddDays(1);
        var lines = await GetPostedLinesAsync(groupId, end, ct);
        var precision = (await GetContextAsync(groupId, toDate, ct)).Precision;
        return BuildMonthlyIncomeExpense(lines, fromDate.Date, toDate.Date, precision);
    }

    public async Task<BalanceSheetResponse> GetBalanceSheetAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        var context = await GetContextAsync(groupId, asOfDate, ct);
        var end = asOfDate.Date.AddDays(1);
        var lines = await GetPostedLinesAsync(groupId, end, ct);

        var accountRows = await db.FinancialAccounts.AsNoTracking().Where(x => x.GroupId == groupId)
            .OrderBy(x => x.AccountCode).ToListAsync(ct);
        var balances = lines.GroupBy(x => x.AccountId).ToDictionary(g => g.Key, g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) });

        var reportLines = new List<BalanceSheetLineResponse>();
        decimal assets = 0m, liabilities = 0m, equity = 0m, income = 0m, expense = 0m;

        foreach (var account in accountRows)
        {
            if (!balances.TryGetValue(account.Id, out var b)) continue;
            var normal = account.AccountType switch
            {
                "Asset" or "Cash" or "Bank" or "Other" => b.Debit - b.Credit,
                "Liability" or "Equity" or "Income" => b.Credit - b.Debit,
                "Expense" => b.Debit - b.Credit,
                _ => b.Debit - b.Credit
            };
            if (normal == 0m) continue;
            reportLines.Add(new BalanceSheetLineResponse(account.AccountType, account.AccountCode, account.AccountName, normal));
            switch (account.AccountType)
            {
                case "Asset": case "Cash": case "Bank": case "Other": assets += normal; break;
                case "Liability": liabilities += normal; break;
                case "Equity": equity += normal; break;
                case "Income": income += normal; break;
                case "Expense": expense += normal; break;
            }
        }

        var netProfit = income - expense;
        return new BalanceSheetResponse(groupId, asOfDate.Date, Round(assets, context.Precision),
            Round(liabilities, context.Precision), Round(equity, context.Precision), Round(netProfit, context.Precision),
            Round(liabilities + equity + netProfit, context.Precision), reportLines);
    }

    public async Task<MemberStatementResponse> GetMemberStatementAsync(long groupId, long memberId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        if (memberId <= 0)
            throw new ReportValidationException("memberId is required for the member statement report.");

        var context = await GetContextAsync(groupId, toDate ?? fromDate ?? DateTime.Today, ct);
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(x => x.Id == memberId && x.GroupId == groupId, ct)
            ?? throw new ReportValidationException("The selected member does not belong to the selected group.");

        var query = db.FinancialTransactions.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.MemberId == memberId && x.Status == "Posted");
        if (fromDate.HasValue) query = query.Where(x => x.TransactionDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.TransactionDate < toDate.Value.Date.AddDays(1));
        var txns = await query.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id)
            .Include(x => x.Details).ThenInclude(x => x.FinancialAccount).Take(5000).ToListAsync(ct);

        decimal before = 0m;
        if (fromDate.HasValue)
        {
            var beforeQuery = db.FinancialTransactions.AsNoTracking()
                .Where(x => x.GroupId == groupId && x.MemberId == memberId && x.Status == "Posted")
                .Where(x => x.TransactionDate < fromDate.Value.Date);
            before = await beforeQuery.SelectMany(x => x.Details)
                .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;
        }

        var entries = txns.SelectMany(t => t.Details.Select(d => new MemberStatementEntryResponse(
            t.TransactionDate, t.TransactionNumber, t.TransactionType,
            d.Description ?? t.Description ?? string.Empty, d.DebitAmount, d.CreditAmount, t.ReferenceNumber))).ToList();

        var distQuery = db.ProfitDistributionDetails.AsNoTracking().Include(x => x.ProfitDistribution)
            .Where(x => x.MemberId == memberId && x.ProfitDistribution.GroupId == groupId && x.ProfitDistribution.Status == "Distributed");
        if (fromDate.HasValue) distQuery = distQuery.Where(x => x.ProfitDistribution.PeriodEnd >= fromDate.Value.Date);
        if (toDate.HasValue) distQuery = distQuery.Where(x => x.ProfitDistribution.PeriodStart <= toDate.Value.Date);
        var distributions = await distQuery.ToListAsync(ct);
        entries.AddRange(distributions.Select(d => new MemberStatementEntryResponse(
            d.ProfitDistribution.DistributedDate ?? d.ProfitDistribution.PeriodEnd,
            $"PD-{d.ProfitDistributionId:00000000}", "INTEREST_DISTRIBUTION",
            "Profit distribution", 0m, d.DistributedAmount, null)));

        return new MemberStatementResponse(groupId, memberId, member.MemberNumber, member.FullName,
            fromDate, toDate, Round(before, context.Precision),
            entries.OrderBy(x => x.TransactionDate).ThenBy(x => x.TransactionNumber).ToArray());
    }

    private async Task<ReportContext> GetContextAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        if (groupId <= 0) throw new ReportValidationException("GroupId is required.");
        var group = await db.Groups.AsNoTracking().Include(x => x.Setting)
            .SingleOrDefaultAsync(x => x.Id == groupId && x.IsActive, ct)
            ?? throw new ReportValidationException("The selected group does not exist or is inactive.");
        var count = await db.Groups.CountAsync(x => x.IsActive, ct);
        if (count != 1) throw new ReportValidationException("The system requires exactly one active configured group.");
        return new ReportContext(group.Code, group.Name, group.CurrencyCode, group.Setting?.MonetaryPrecision ?? 2, group.Setting?.ProfitReserveRate ?? 10m);
    }

    private async Task<decimal> GetFundBalanceAsync(long groupId, DateTime endExclusive, CancellationToken ct)
    {
        return await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == groupId && x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < endExclusive &&
                        (x.FinancialAccount.AccountType == "Cash" || x.FinancialAccount.AccountType == "Bank" || x.FinancialAccount.AccountType == "Other"))
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;
    }

    private async Task<List<PostedLine>> GetPostedLinesAsync(long groupId, DateTime endExclusive, CancellationToken ct)
    {
        return await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == groupId && x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < endExclusive)
            .Select(x => new PostedLine(x.FinancialAccountId, x.FinancialAccount.AccountCode, x.FinancialAccount.AccountType,
                x.FinancialTransaction.TransactionType, x.FinancialTransaction.TransactionDate, x.DebitAmount, x.CreditAmount))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyCollection<OverdueRow>> GetOverdueRowsAsync(long groupId, DateTime date, CancellationToken ct)
    {
        var rows = await db.LoanSchedules.AsNoTracking()
            .Where(x => x.Loan.GroupId == groupId && x.Loan.Status == "Active" && x.DueDate < date)
            .Select(x => new { x.LoanId, x.PrincipalDue, x.PrincipalPaid, x.InterestDue, x.InterestPaid, x.PenaltyDue, x.PenaltyPaid })
            .ToListAsync(ct);
        return rows.Select(x => new OverdueRow(x.LoanId,
            Math.Max(0m, x.PrincipalDue - x.PrincipalPaid) + Math.Max(0m, x.InterestDue - x.InterestPaid) +
            Math.Max(0m, x.PenaltyDue - x.PenaltyPaid))).Where(x => x.Outstanding > 0m).ToArray();
    }

    private static IReadOnlyCollection<IncomeExpenseReportResponse> BuildMonthlyIncomeExpense(
        IReadOnlyCollection<PostedLine> lines, DateTime fromDate, DateTime toDate, int precision)
    {
        var results = new List<IncomeExpenseReportResponse>();
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
        var finalMonth = new DateTime(toDate.Year, toDate.Month, 1);
        while (cursor <= finalMonth)
        {
            var monthStart = cursor;
            var monthEnd = cursor.AddMonths(1);
            var monthLines = lines.Where(x => x.TransactionDate >= monthStart && x.TransactionDate < monthEnd)
                .Select(x => new ProfitLineInput(x.Code, x.Type, x.TransactionType, x.Debit, x.Credit)).ToArray();
            var result = ProfitCalculationEngine.Calculate(monthLines, 0m, precision);
            results.Add(new IncomeExpenseReportResponse(cursor.Month, cursor.ToString("MMM"), result.TotalIncome,
                result.TotalCosts, result.NetProfit));
            cursor = cursor.AddMonths(1);
        }
        return results;
    }

    private static CollectionDueReportResponse MapCollection(GroupFinance.Core.Entities.ContributionSchedule x)
    {
        var outstanding = Math.Max(0m, x.DueAmount - x.PaidAmount) + Math.Max(0m, x.LateFeeAmount - x.WaivedLateFeeAmount);
        return new CollectionDueReportResponse(x.Id, x.MemberId, x.Member.MemberNumber, x.Member.FullName, x.PeriodStart,
            x.PeriodEnd, x.DueDate, x.DueAmount, x.PaidAmount, x.LateFeeAmount, x.WaivedLateFeeAmount,
            outstanding, x.Status);
    }

    private static void ValidateRange(DateTime fromDate, DateTime toDate)
    {
        if (toDate.Date < fromDate.Date) throw new ReportValidationException("To date cannot be earlier than from date.");
    }

    private static decimal Round(decimal value, int precision) => decimal.Round(value, Math.Clamp(precision, 0, 6), MidpointRounding.AwayFromZero);
    private sealed record ReportContext(string GroupCode, string GroupName, string CurrencyCode, int Precision, decimal ReserveRate);
    private sealed record PostedLine(long AccountId, string Code, string Type, string TransactionType, DateTime TransactionDate, decimal Debit, decimal Credit);
    private sealed record OverdueRow(long LoanId, decimal Outstanding);
}
