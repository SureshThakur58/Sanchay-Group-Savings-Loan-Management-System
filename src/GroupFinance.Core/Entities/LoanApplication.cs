namespace GroupFinance.Core.Entities;

public sealed class LoanApplication : BaseEntity
{
    public long MemberId { get; set; }
    public string LoanApplicationNumber { get; set; } = string.Empty;
    public DateTime ApplicationDate { get; set; }
    public decimal RequestedAmount { get; set; }
    public int RequestedDurationMonths { get; set; }
    public decimal ProposedInterestRate { get; set; }
    public string ProposedInterestMethod { get; set; } = "ReducingBalance";
    public decimal RequestedEmi { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? DecisionDate { get; set; }
    public long? DecidedByUserId { get; set; }
    public string? DecisionRemarks { get; set; }

    public Member Member { get; set; } = null!;
    public ICollection<LoanGuarantor> Guarantors { get; set; } = [];
    public Loan? Loan { get; set; }
}
