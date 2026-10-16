namespace GroupFinance.Core.Entities;

public sealed class UserRole : BaseEntity
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
