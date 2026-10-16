using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record ReportSummaryModel(long GroupId, string GroupCode, string GroupName, DateTime AsOfDate, string CurrencyCode,
    int TotalMembers, int ActiveMembers, decimal TotalContributions, decimal TotalLoans, decimal OutstandingLoans,
    decimal InterestEarned, decimal Expenses, decimal NetProfit, decimal AvailableCash, int OverdueLoans, decimal OverdueAmount);

public sealed record MemberReportModel(long Id, string MemberNumber, string FullName, string? MobileNumber, string? Email,
    DateTime JoinDate, DateTime? ExitDate, bool IsActive, decimal TotalContribution, decimal TotalLoan, decimal PrincipalOutstanding);

public sealed record ContributionReportModel(long PaymentId, long MemberId, string MemberNumber, string MemberName,
    DateTime TransactionDate, decimal Amount, string PaymentMethod, string? ReferenceNumber, string? Description, string Status);

public sealed record CollectionDueReportModel(long ScheduleId, long MemberId, string MemberNumber, string MemberName,
    DateTime PeriodStart, DateTime PeriodEnd, DateTime DueDate, decimal DueAmount, decimal PaidAmount,
    decimal LateFeeAmount, decimal WaivedLateFeeAmount, decimal OutstandingAmount, string Status);

public sealed record LoanRegisterReportModel(long LoanId, string LoanNumber, long MemberId, string MemberNumber, string MemberName,
    DateTime DisbursementDate, decimal PrincipalAmount, decimal InterestRate, string InterestMethod, int DurationMonths,
    decimal PrincipalPaid, decimal InterestPaid, decimal PenaltyPaid, decimal OutstandingPrincipal, string Status);

public sealed record LoanRepaymentReportModel(long PaymentId, long LoanId, string LoanNumber, long MemberId, string MemberNumber,
    string MemberName, DateTime TransactionDate, decimal Amount, decimal PrincipalAllocated, decimal InterestAllocated,
    decimal PenaltyAllocated, decimal OverpaymentAmount, string PaymentMethod, string? ReferenceNumber, string? Description, string Status);

public sealed record LoanInstallmentReportModel(long ScheduleId, long LoanId, string LoanNumber, long MemberId, string MemberNumber,
    string MemberName, int InstallmentNumber, DateTime DueDate, decimal OpeningPrincipal, decimal PrincipalDue,
    decimal InterestDue, decimal PenaltyDue, decimal TotalDue, decimal PrincipalPaid, decimal InterestPaid,
    decimal PenaltyPaid, decimal OutstandingAmount, string Status);

public sealed record OverdueReportModel(long LoanId, string LoanNumber, long MemberId, string MemberNumber, string MemberName,
    int InstallmentNumber, DateTime DueDate, int DaysOverdue, decimal PrincipalOutstanding, decimal InterestOutstanding,
    decimal PenaltyOutstanding, decimal OutstandingAmount);

public sealed record InterestIncomeReportModel(long InterestCalculationId, long LoanId, string LoanNumber, long MemberId,
    string MemberName, DateTime PeriodStart, DateTime PeriodEnd, decimal OpeningPrincipal, decimal InterestRate,
    decimal InterestAmount, string CalculationMethod, bool IsPosted);

public sealed record ProfitDistributionReportModel(long DistributionId, DateTime PeriodStart, DateTime PeriodEnd,
    decimal NetProfit, decimal ReserveAmount, decimal DistributableAmount, string DistributionMethod, string Status,
    DateTime? DistributedDate, long MemberId, string MemberNumber, string MemberName, decimal DistributedAmount);

public sealed record ExpenseReportModel(long ExpenseId, string ExpenseNumber, DateTime ExpenseDate, long ExpenseCategoryId,
    string ExpenseCategoryName, decimal Amount, string PaymentMethod, long FinancialAccountId, string FinancialAccountCode,
    string FinancialAccountName, string? ReferenceNumber, string Description, string Status);

public sealed record AssetReportModel(long AssetId, string AssetCode, string AssetName, string Category, DateTime AcquisitionDate,
    decimal AcquisitionCost, decimal BookValue, string? Location, string? Description, bool IsActive, string Status);

public sealed record CashBankReportModel(long FinancialAccountId, string AccountCode, string AccountName, string AccountType,
    decimal TotalDebit, decimal TotalCredit, decimal Balance);

public sealed record IncomeExpenseReportModel(int Month, string MonthName, decimal Income, decimal Expense, decimal Profit);

public sealed record BalanceSheetLineModel(string AccountType, string AccountCode, string AccountName, decimal Balance);
public sealed record BalanceSheetModel(long GroupId, DateTime AsOfDate, decimal TotalAssets, decimal TotalLiabilities,
    decimal TotalEquity, decimal NetProfit, decimal TotalLiabilitiesAndEquity, IReadOnlyList<BalanceSheetLineModel> Lines);

public sealed record MemberStatementEntryModel(DateTime TransactionDate, string TransactionNumber, string TransactionType,
    string Description, decimal DebitAmount, decimal CreditAmount, string? ReferenceNumber);
public sealed record MemberStatementModel(long GroupId, long MemberId, string MemberNumber, string MemberName,
    DateTime? FromDate, DateTime? ToDate, decimal OpeningNetMovement, IReadOnlyList<MemberStatementEntryModel> Entries);

public sealed class ReportsApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public Task<(bool Success, string? Error, ReportSummaryModel? Data)> GetSummaryAsync(long groupId, DateTime? asOfDate) =>
        GetAsync<ReportSummaryModel>(Build("api/reports/summary", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<MemberReportModel>? Data)> GetMembersAsync(long groupId, DateTime? asOfDate) =>
        GetListAsync<MemberReportModel>(Build("api/reports/members", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<ContributionReportModel>? Data)> GetContributionsAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? memberId) =>
        GetListAsync<ContributionReportModel>(Build("api/reports/contributions", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate), ("memberId", memberId)));

    public Task<(bool Success, string? Error, IReadOnlyList<CollectionDueReportModel>? Data)> GetCollectionDueAsync(long groupId, DateTime fromDate, DateTime toDate) =>
        GetListAsync<CollectionDueReportModel>(Build("api/reports/collection-due", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<CollectionDueReportModel>? Data)> GetCollectionOutstandingAsync(long groupId, DateTime? asOfDate) =>
        GetListAsync<CollectionDueReportModel>(Build("api/reports/collection-outstanding", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<LoanRegisterReportModel>? Data)> GetLoanRegisterAsync(long groupId, DateTime? asOfDate) =>
        GetListAsync<LoanRegisterReportModel>(Build("api/reports/loan-register", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<LoanRegisterReportModel>? Data)> GetLoanOutstandingAsync(long groupId, DateTime? asOfDate) =>
        GetListAsync<LoanRegisterReportModel>(Build("api/reports/loan-outstanding", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<LoanRepaymentReportModel>? Data)> GetLoanRepaymentsAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? memberId, long? loanId) =>
        GetListAsync<LoanRepaymentReportModel>(Build("api/reports/loan-repayments", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate), ("memberId", memberId), ("loanId", loanId)));

    public Task<(bool Success, string? Error, IReadOnlyList<LoanInstallmentReportModel>? Data)> GetLoanInstallmentsAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? loanId) =>
        GetListAsync<LoanInstallmentReportModel>(Build("api/reports/loan-installments", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate), ("loanId", loanId)));

    public Task<(bool Success, string? Error, IReadOnlyList<OverdueReportModel>? Data)> GetOverdueAsync(long groupId, DateTime? asOfDate) =>
        GetListAsync<OverdueReportModel>(Build("api/reports/overdue", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<InterestIncomeReportModel>? Data)> GetInterestIncomeAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? loanId) =>
        GetListAsync<InterestIncomeReportModel>(Build("api/reports/interest-income", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate), ("loanId", loanId)));

    public Task<(bool Success, string? Error, IReadOnlyList<ProfitDistributionReportModel>? Data)> GetProfitDistributionAsync(long groupId, DateTime? fromDate, DateTime? toDate) =>
        GetListAsync<ProfitDistributionReportModel>(Build("api/reports/profit-distribution", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<ExpenseReportModel>? Data)> GetExpensesAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? categoryId) =>
        GetListAsync<ExpenseReportModel>(Build("api/reports/expenses", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate), ("categoryId", categoryId)));

    public Task<(bool Success, string? Error, IReadOnlyList<AssetReportModel>? Data)> GetAssetsAsync(long groupId, DateTime? asOfDate, bool includeInactive) =>
        GetListAsync<AssetReportModel>(Build("api/reports/assets", ("groupId", groupId), ("asOfDate", asOfDate), ("includeInactive", includeInactive)));

    public Task<(bool Success, string? Error, IReadOnlyList<CashBankReportModel>? Data)> GetCashBankAsync(long groupId, DateTime? asOfDate) =>
        GetListAsync<CashBankReportModel>(Build("api/reports/cash-bank", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, IReadOnlyList<IncomeExpenseReportModel>? Data)> GetIncomeExpenseAsync(long groupId, DateTime fromDate, DateTime toDate) =>
        GetListAsync<IncomeExpenseReportModel>(Build("api/reports/income-expense", ("groupId", groupId), ("fromDate", fromDate), ("toDate", toDate)));

    public Task<(bool Success, string? Error, BalanceSheetModel? Data)> GetBalanceSheetAsync(long groupId, DateTime? asOfDate) =>
        GetAsync<BalanceSheetModel>(Build("api/reports/balance-sheet", ("groupId", groupId), ("asOfDate", asOfDate)));

    public Task<(bool Success, string? Error, MemberStatementModel? Data)> GetMemberStatementAsync(long groupId, long memberId, DateTime? fromDate, DateTime? toDate) =>
        GetAsync<MemberStatementModel>(Build("api/reports/member-statement", ("groupId", groupId), ("memberId", memberId), ("fromDate", fromDate), ("toDate", toDate)));

    private async Task<(bool Success, string? Error, IReadOnlyList<T>? Data)> GetListAsync<T>(string uri)
    {
        var result = await GetAsync<List<T>>(uri);
        return (result.Success, result.Error, result.Data);
    }

    private async Task<(bool Success, string? Error, T? Data)> GetAsync<T>(string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response), default);
        var data = await response.Content.ReadFromJsonAsync<T>();
        return data is null ? (false, "The API returned an invalid response.", default) : (true, null, data);
    }

    private static string Build(string path, params (string Name, object? Value)[] values)
    {
        var query = string.Join("&", values.Where(x => x.Value is not null).Select(x =>
            $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(ToQueryValue(x.Value!))}"));
        return query.Length == 0 ? path : $"{path}?{query}";
    }

    private static string ToQueryValue(object value) => value switch
    {
        DateTime date => date.ToString("O"),
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return error?.Message ?? $"Request failed with HTTP {(int)response.StatusCode}.";
        }
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }

    private sealed record ErrorResponse(string Message);
}
