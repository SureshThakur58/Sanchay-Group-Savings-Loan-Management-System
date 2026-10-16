namespace GroupFinance.Core.Entities;

public sealed class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
