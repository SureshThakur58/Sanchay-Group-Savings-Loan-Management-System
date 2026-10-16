namespace GroupFinance.Core.Entities;

public sealed class FinancialTransaction : BaseEntity
{
    public long GroupId { get; set; }
    public long? MemberId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Status { get; set; } = "Posted";
    public decimal Amount { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public long? ReversalOfTransactionId { get; set; }

    public Group Group { get; set; } = null!;
    public Member? Member { get; set; }
    public FinancialTransaction? ReversalOfTransaction { get; set; }
    public FinancialTransaction? ReversedByTransaction { get; set; }
    public ICollection<TransactionDetail> Details { get; set; } = [];
    public ICollection<ContributionPayment> ContributionPayments { get; set; } = [];
    public ICollection<LoanPayment> LoanPayments { get; set; } = [];
}
