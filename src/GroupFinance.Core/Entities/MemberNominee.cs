namespace GroupFinance.Core.Entities;

public sealed class MemberNominee : BaseEntity
{
    public long MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? MobileNumber { get; set; }
    public string? Address { get; set; }
    public decimal? SharePercentage { get; set; }
    public bool IsActive { get; set; } = true;

    public Member Member { get; set; } = null!;
}
