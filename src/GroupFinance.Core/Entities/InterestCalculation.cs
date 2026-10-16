namespace GroupFinance.Core.Entities;

public sealed class InterestCalculation : BaseEntity
{
    public long LoanId { get; set; }
    public DateTime CalculationDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal InterestAmount { get; set; }
    public string CalculationMethod { get; set; } = "ReducingBalance";
    public bool IsPosted { get; set; }

    public Loan Loan { get; set; } = null!;
}
