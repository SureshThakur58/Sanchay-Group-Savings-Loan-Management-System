namespace GroupFinance.Core.Entities;

public sealed class FinancialAccount : BaseEntity
{
    public long GroupId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public bool IsSystemAccount { get; set; }
    public bool IsActive { get; set; } = true;

    public Group Group { get; set; } = null!;
    public ICollection<TransactionDetail> TransactionDetails { get; set; } = [];
}
