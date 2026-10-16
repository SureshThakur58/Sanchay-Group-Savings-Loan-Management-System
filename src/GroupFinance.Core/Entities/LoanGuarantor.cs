namespace GroupFinance.Core.Entities;

public sealed class LoanGuarantor : BaseEntity
{
    public long? LoanId { get; set; }
    public long MemberId { get; set; }
    public long? LoanApplicationId { get; set; }
    public DateTime ProvidedAt { get; set; }
    public string Status { get; set; } = "Active";

    public Loan Loan { get; set; } = null!;
    public Member Member { get; set; } = null!;
    public LoanApplication? LoanApplication { get; set; }
}
