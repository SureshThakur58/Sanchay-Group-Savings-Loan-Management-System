namespace GroupFinance.Core.Entities;

public sealed class SystemSetting : BaseEntity
{
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
}
