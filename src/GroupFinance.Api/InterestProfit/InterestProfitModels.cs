namespace GroupFinance.Api.InterestProfit;

public sealed record CalculateInterestRequest(
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    long? LoanId = null);

public sealed record InterestCalculationResponse(
    long Id,
    long LoanId,
    string LoanNumber,
    long MemberId,
    string MemberName,
    DateTime CalculationDate,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningPrincipal,
    decimal InterestRate,
    decimal InterestAmount,
    string CalculationMethod,
    bool IsPosted);

public sealed record ProfitCalculationRequest(
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed record ProfitCalculationResponse(
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal LoanInterestIncome,
    decimal InvestmentIncome,
    decimal OtherIncome,
    decimal TotalIncome,
    decimal Expenses,
    decimal OtherCosts,
    decimal TotalCosts,
    decimal NetProfit,
    decimal ReserveRate,
    decimal ReserveAmount,
    decimal DistributableAmount);
