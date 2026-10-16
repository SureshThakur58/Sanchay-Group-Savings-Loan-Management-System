namespace GroupFinance.Core.Entities;

public sealed class ProfitDistribution : BaseEntity
{
    public long GroupId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ReserveAmount { get; set; }
    public decimal DistributableAmount { get; set; }
    public string DistributionMethod { get; set; } = "ContributionBased";
    public string Status { get; set; } = "Draft";
    public DateTime? DistributedDate { get; set; }

    public Group Group { get; set; } = null!;
    public ICollection<ProfitDistributionDetail> Details { get; set; } = [];
}
