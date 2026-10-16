namespace GroupFinance.Core.Entities;

public sealed class Asset : BaseEntity
{
    public long GroupId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public DateTime AcquisitionDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal BookValue { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Group Group { get; set; } = null!;
    public ICollection<AssetTransaction> Transactions { get; set; } = [];
}
