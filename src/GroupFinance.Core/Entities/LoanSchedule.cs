namespace GroupFinance.Core.Entities;

public sealed class LoanSchedule : BaseEntity
{
    public long LoanId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OpeningPrincipal { get; set; }
    public decimal PrincipalDue { get; set; }
    public decimal InterestDue { get; set; }
    public decimal PenaltyDue { get; set; }
    public decimal TotalDue { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal PenaltyPaid { get; set; }
    public string Status { get; set; } = "Due";

    public Loan Loan { get; set; } = null!;
    public ICollection<LoanPayment> Payments { get; set; } = [];
}
