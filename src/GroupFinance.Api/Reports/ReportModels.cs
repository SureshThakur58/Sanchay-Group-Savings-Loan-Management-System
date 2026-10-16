namespace GroupFinance.Api.Reports;

public sealed record ReportSummaryResponse(
    long GroupId, string GroupCode, string GroupName, DateTime AsOfDate, string CurrencyCode,
    int TotalMembers, int ActiveMembers, decimal TotalContributions, decimal TotalLoans,
    decimal OutstandingLoans, decimal InterestEarned, decimal Expenses, decimal NetProfit,
    decimal AvailableCash, int OverdueLoans, decimal OverdueAmount);

public sealed record MemberReportResponse(
    long Id, string MemberNumber, string FullName, string? MobileNumber, string? Email,
    DateTime JoinDate, DateTime? ExitDate, bool IsActive, decimal TotalContribution,
    decimal TotalLoan, decimal PrincipalOutstanding);

public sealed record ContributionReportResponse(
    long PaymentId, long MemberId, string MemberNumber, string MemberName,
    DateTime TransactionDate, decimal Amount, string PaymentMethod, string? ReferenceNumber,
    string? Description, string Status);

public sealed record CollectionDueReportResponse(
    long ScheduleId, long MemberId, string MemberNumber, string MemberName,
    DateTime PeriodStart, DateTime PeriodEnd, DateTime DueDate, decimal DueAmount,
    decimal PaidAmount, decimal LateFeeAmount, decimal WaivedLateFeeAmount,
    decimal OutstandingAmount, string Status);

public sealed record LoanRegisterReportResponse(
    long LoanId, string LoanNumber, long MemberId, string MemberNumber, string MemberName,
    DateTime DisbursementDate, decimal PrincipalAmount, decimal InterestRate,
    string InterestMethod, int DurationMonths, decimal PrincipalPaid, decimal InterestPaid,
    decimal PenaltyPaid, decimal OutstandingPrincipal, string Status);

public sealed record LoanRepaymentReportResponse(
    long PaymentId, long LoanId, string LoanNumber, long MemberId, string MemberNumber,
    string MemberName, DateTime TransactionDate, decimal Amount, decimal PrincipalAllocated,
    decimal InterestAllocated, decimal PenaltyAllocated, decimal OverpaymentAmount,
    string PaymentMethod, string? ReferenceNumber, string? Description, string Status);

public sealed record LoanInstallmentReportResponse(
    long ScheduleId, long LoanId, string LoanNumber, long MemberId, string MemberNumber,
    string MemberName, int InstallmentNumber, DateTime DueDate, decimal OpeningPrincipal,
    decimal PrincipalDue, decimal InterestDue, decimal PenaltyDue, decimal TotalDue,
    decimal PrincipalPaid, decimal InterestPaid, decimal PenaltyPaid, decimal OutstandingAmount,
    string Status);

public sealed record OverdueReportResponse(
    long LoanId, string LoanNumber, long MemberId, string MemberNumber, string MemberName,
    int InstallmentNumber, DateTime DueDate, int DaysOverdue, decimal PrincipalOutstanding,
    decimal InterestOutstanding, decimal PenaltyOutstanding, decimal OutstandingAmount);

public sealed record InterestIncomeReportResponse(
    long InterestCalculationId, long LoanId, string LoanNumber, long MemberId,
    string MemberName, DateTime PeriodStart, DateTime PeriodEnd, decimal OpeningPrincipal,
    decimal InterestRate, decimal InterestAmount, string CalculationMethod, bool IsPosted);

public sealed record ProfitDistributionReportResponse(
    long DistributionId, DateTime PeriodStart, DateTime PeriodEnd, decimal NetProfit,
    decimal ReserveAmount, decimal DistributableAmount, string DistributionMethod,
    string Status, DateTime? DistributedDate, long MemberId, string MemberNumber,
    string MemberName, decimal DistributedAmount);

public sealed record ExpenseReportResponse(
    long ExpenseId, string ExpenseNumber, DateTime ExpenseDate, long ExpenseCategoryId,
    string ExpenseCategoryName, decimal Amount, string PaymentMethod,
    long FinancialAccountId, string FinancialAccountCode, string FinancialAccountName,
    string? ReferenceNumber, string Description, string Status);

public sealed record AssetReportResponse(
    long AssetId, string AssetCode, string AssetName, string Category,
    DateTime AcquisitionDate, decimal AcquisitionCost, decimal BookValue,
    string? Location, string? Description, bool IsActive, string Status);

public sealed record CashBankReportResponse(
    long FinancialAccountId, string AccountCode, string AccountName, string AccountType,
    decimal TotalDebit, decimal TotalCredit, decimal Balance);

public sealed record IncomeExpenseReportResponse(
    int Month, string MonthName, decimal Income, decimal Expense, decimal Profit);

public sealed record BalanceSheetLineResponse(
    string AccountType, string AccountCode, string AccountName, decimal Balance);

public sealed record BalanceSheetResponse(
    long GroupId, DateTime AsOfDate, decimal TotalAssets, decimal TotalLiabilities,
    decimal TotalEquity, decimal NetProfit, decimal TotalLiabilitiesAndEquity,
    IReadOnlyCollection<BalanceSheetLineResponse> Lines);

public sealed record MemberStatementResponse(
    long GroupId, long MemberId, string MemberNumber, string MemberName,
    DateTime? FromDate, DateTime? ToDate, decimal OpeningNetMovement,
    IReadOnlyCollection<MemberStatementEntryResponse> Entries);

public sealed record MemberStatementEntryResponse(
    DateTime TransactionDate, string TransactionNumber, string TransactionType,
    string Description, decimal DebitAmount, decimal CreditAmount, string? ReferenceNumber);

public sealed class ReportValidationException(string message) : Exception(message);
