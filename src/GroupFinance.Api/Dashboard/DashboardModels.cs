namespace GroupFinance.Api.Dashboard;

public sealed record DashboardFundDistributionResponse(
    decimal Cash,
    decimal Loans,
    decimal Assets,
    decimal Investments);

public sealed record DashboardMonthlyCollectionResponse(
    int Month,
    string MonthName,
    decimal Amount);

public sealed record DashboardLoanStatusResponse(
    string Status,
    int Count);

public sealed record DashboardIncomeExpenseResponse(
    int Month,
    string MonthName,
    decimal Income,
    decimal Expense,
    decimal Profit);

public sealed record DashboardResponse(
    long GroupId,
    DateTime AsOfDate,
    string CurrencyCode,
    int TotalMembers,
    int ActiveMembers,
    decimal TotalContributions,
    decimal TotalLoans,
    decimal OutstandingLoans,
    decimal InterestEarned,
    decimal Expenses,
    decimal NetProfit,
    decimal AvailableCash,
    int OverdueLoans,
    decimal OverdueAmount,
    DashboardFundDistributionResponse FundDistribution,
    IReadOnlyCollection<DashboardMonthlyCollectionResponse> MonthlyCollections,
    IReadOnlyCollection<DashboardLoanStatusResponse> LoanStatus,
    IReadOnlyCollection<DashboardIncomeExpenseResponse> IncomeVsExpense);
