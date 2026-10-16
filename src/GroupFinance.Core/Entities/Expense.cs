namespace GroupFinance.Core.Entities;

public sealed class Expense : BaseEntity
{
    public long GroupId { get; set; }
    public long ExpenseCategoryId { get; set; }
    public string ExpenseNumber { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = "Posted";
    public long? FinancialTransactionId { get; set; }

    public Group Group { get; set; } = null!;
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public FinancialTransaction? FinancialTransaction { get; set; }
}
