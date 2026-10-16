namespace GroupFinance.Core.Entities;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public long? ModifiedByUserId { get; set; }
}
