namespace GroupFinance.Core.Entities;

public sealed class ContributionPayment : BaseEntity
{
    public long ContributionScheduleId { get; set; }
    public long MemberId { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Posted";
    public long? FinancialAccountId { get; set; }
    public long? FinancialTransactionId { get; set; }

    public ContributionSchedule ContributionSchedule { get; set; } = null!;
    public Member Member { get; set; } = null!;
    public FinancialAccount? FinancialAccount { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }
}
