namespace GroupFinance.Core.Entities;

public sealed class Loan : BaseEntity
{
    public long GroupId { get; set; }
    public long MemberId { get; set; }
    public long? LoanApplicationId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public DateTime DisbursementDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public string InterestMethod { get; set; } = "ReducingBalance";
    public int DurationMonths { get; set; }
    public string InstallmentFrequency { get; set; } = "Monthly";
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal PenaltyPaid { get; set; }
    public decimal OverpaymentAmount { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? ClosedDate { get; set; }

    public Group Group { get; set; } = null!;
    public Member Member { get; set; } = null!;
    public LoanApplication? LoanApplication { get; set; }
    public ICollection<LoanSchedule> Schedules { get; set; } = [];
    public ICollection<LoanPayment> Payments { get; set; } = [];
    public ICollection<LoanGuarantor> Guarantors { get; set; } = [];
    public ICollection<InterestCalculation> InterestCalculations { get; set; } = [];
}
