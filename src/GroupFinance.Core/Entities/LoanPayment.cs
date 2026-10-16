namespace GroupFinance.Core.Entities;

public sealed class LoanPayment : BaseEntity
{
    public long LoanId { get; set; }
    public long? LoanScheduleId { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PenaltyAllocated { get; set; }
    public decimal InterestAllocated { get; set; }
    public decimal PrincipalAllocated { get; set; }
    public decimal OverpaymentAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Posted";
    public long? FinancialTransactionId { get; set; }

    public Loan Loan { get; set; } = null!;
    public LoanSchedule? LoanSchedule { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }
}
