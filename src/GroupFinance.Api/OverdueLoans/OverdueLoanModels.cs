namespace GroupFinance.Api.OverdueLoans;

public sealed record EvaluateOverdueLoansRequest(long GroupId, DateTime AsOfDate);

public sealed record OverdueLoanScheduleResponse(
    long LoanScheduleId,
    long LoanId,
    string LoanNumber,
    long MemberId,
    string MemberNumber,
    string MemberName,
    int InstallmentNumber,
    DateTime DueDate,
    int DaysOverdue,
    decimal PrincipalDue,
    decimal InterestDue,
    decimal PenaltyDue,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal PenaltyPaid,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal OutstandingPenalty,
    decimal OutstandingAmount,
    string Status);

public sealed record OverdueLoanSummaryResponse(
    long LoanId,
    string LoanNumber,
    long GroupId,
    long MemberId,
    string MemberNumber,
    string MemberName,
    int OverdueInstallmentCount,
    int MaximumDaysOverdue,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal OutstandingPenalty,
    decimal TotalOverdueAmount,
    DateTime EarliestDueDate,
    string Status,
    IReadOnlyList<OverdueLoanScheduleResponse> Installments);

public sealed record OverdueEvaluationResponse(
    long GroupId,
    DateTime AsOfDate,
    int OverdueInstallmentCount,
    int OverdueLoanCount,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal OutstandingPenalty,
    decimal TotalOverdueAmount);
