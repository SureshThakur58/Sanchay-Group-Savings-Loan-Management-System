namespace GroupFinance.Api.LoanRepayments;

public sealed record LoanRepaymentRequest(
    long FinancialAccountId,
    DateTime TransactionDate,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber = null,
    string? Description = null);

public sealed record LoanRepaymentAllocationResponse(
    long LoanScheduleId,
    int InstallmentNumber,
    DateTime DueDate,
    decimal PenaltyAllocated,
    decimal InterestAllocated,
    decimal PrincipalAllocated);

public sealed record LoanRepaymentResponse(
    long LoanPaymentId,
    long LoanId,
    string LoanNumber,
    long MemberId,
    decimal Amount,
    decimal PenaltyAllocated,
    decimal InterestAllocated,
    decimal PrincipalAllocated,
    decimal OverpaymentAmount,
    string PaymentMethod,
    DateTime TransactionDate,
    string Status,
    long FinancialTransactionId,
    string FinancialTransactionNumber,
    long FinancialAccountId,
    string FinancialAccountCode,
    string FinancialAccountName,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal PenaltyPaid,
    decimal OutstandingPrincipal,
    string LoanStatus,
    IReadOnlyList<LoanRepaymentAllocationResponse> Allocations);

public sealed record LoanPaymentSummaryResponse(
    long Id,
    long LoanId,
    string LoanNumber,
    long MemberId,
    DateTime TransactionDate,
    decimal Amount,
    decimal PenaltyAllocated,
    decimal InterestAllocated,
    decimal PrincipalAllocated,
    decimal OverpaymentAmount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Description,
    string Status,
    long? FinancialTransactionId);
