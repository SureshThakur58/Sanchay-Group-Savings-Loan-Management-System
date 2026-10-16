namespace GroupFinance.Core.Entities;

public sealed class ContributionPlan : BaseEntity
{
    public long GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Frequency { get; set; } = "Quarterly";
    public int IntervalMonths { get; set; } = 3;
    public int DueDay { get; set; } = 1;
    public decimal LateFeeRate { get; set; } = 1m;
    public bool IsActive { get; set; } = true;

    public Group Group { get; set; } = null!;
    public ICollection<ContributionSchedule> Schedules { get; set; } = [];
}
