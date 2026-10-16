namespace GroupFinance.Core.Entities;

public sealed class ProfitDistributionDetail : BaseEntity
{
    public long ProfitDistributionId { get; set; }
    public long MemberId { get; set; }
    public decimal EligibilityFactor { get; set; }
    public decimal CalculatedShare { get; set; }
    public decimal DistributedAmount { get; set; }

    public ProfitDistribution ProfitDistribution { get; set; } = null!;
    public Member Member { get; set; } = null!;
}
