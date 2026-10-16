namespace GroupFinance.Api.LoanCalculations;

public sealed record LoanCalculationRequest(
    long GroupId,
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int DurationMonths,
    string InterestMethod,
    DateTime FirstDueDate);

public sealed record LoanScheduleLineResponse(
    int InstallmentNumber,
    DateTime DueDate,
    decimal OpeningPrincipal,
    decimal PrincipalDue,
    decimal InterestDue,
    decimal TotalDue,
    decimal ClosingPrincipal);

public sealed record LoanCalculationResponse(
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int DurationMonths,
    string InterestMethod,
    decimal MonthlyInstallment,
    decimal TotalInterest,
    decimal TotalRepayment,
    IReadOnlyCollection<LoanScheduleLineResponse> Schedule);
