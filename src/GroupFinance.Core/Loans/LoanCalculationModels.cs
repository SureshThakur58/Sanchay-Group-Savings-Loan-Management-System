namespace GroupFinance.Core.Loans;

public static class LoanInterestMethods
{
    public const string Flat = "Flat";
    public const string ReducingBalance = "ReducingBalance";

    public static bool IsSupported(string? method) =>
        method is not null &&
        (method.Equals(Flat, StringComparison.OrdinalIgnoreCase) ||
         method.Equals(ReducingBalance, StringComparison.OrdinalIgnoreCase));
}

public sealed record LoanCalculationRequest(
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int DurationMonths,
    string InterestMethod,
    DateTime FirstDueDate,
    int MonetaryPrecision);

public sealed record LoanScheduleLine(
    int InstallmentNumber,
    DateTime DueDate,
    decimal OpeningPrincipal,
    decimal PrincipalDue,
    decimal InterestDue,
    decimal TotalDue,
    decimal ClosingPrincipal);

public sealed record LoanCalculationResult(
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int DurationMonths,
    string InterestMethod,
    decimal MonthlyInstallment,
    decimal TotalInterest,
    decimal TotalRepayment,
    IReadOnlyList<LoanScheduleLine> Schedule);
