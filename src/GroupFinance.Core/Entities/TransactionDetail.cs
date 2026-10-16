namespace GroupFinance.Core.Entities;

public sealed class TransactionDetail : BaseEntity
{
    public long FinancialTransactionId { get; set; }
    public long FinancialAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }

    public FinancialTransaction FinancialTransaction { get; set; } = null!;
    public FinancialAccount FinancialAccount { get; set; } = null!;
}
