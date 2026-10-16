namespace GroupFinance.Core.Entities;

public sealed class ExpenseCategory : BaseEntity
{
    public long GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Group Group { get; set; } = null!;
    public ICollection<Expense> Expenses { get; set; } = [];
}
