namespace GroupFinance.Core.Entities;

public sealed class ContributionSchedule : BaseEntity
{
    public long ContributionPlanId { get; set; }
    public long MemberId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime DueDate { get; set; }
    public decimal DueAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal LateFeeAmount { get; set; }
    public decimal WaivedLateFeeAmount { get; set; }
    public string Status { get; set; } = "Due";

    public ContributionPlan ContributionPlan { get; set; } = null!;
    public Member Member { get; set; } = null!;
    public ICollection<ContributionPayment> Payments { get; set; } = [];
}
