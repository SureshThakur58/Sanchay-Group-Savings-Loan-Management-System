namespace GroupFinance.Core.Entities;

public sealed class AssetTransaction : BaseEntity
{
    public long AssetId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = "Purchase";
    public decimal Amount { get; set; }
    public decimal? BookValueAtSale { get; set; }
    public decimal? GainLoss { get; set; }
    public string? Description { get; set; }
    public long? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }

    public Asset Asset { get; set; } = null!;
}
