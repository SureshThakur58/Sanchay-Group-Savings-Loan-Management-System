namespace GroupFinance.Core.Entities;

public sealed class Group : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime FormationDate { get; set; }
    public string? RegistrationNumber { get; set; }
    public string CurrencyCode { get; set; } = "NPR";
    public bool IsActive { get; set; } = true;
    public int SingleGroupKey { get; set; } = 1;

    public GroupSetting? Setting { get; set; }
    public ICollection<Member> Members { get; set; } = [];
    public ICollection<ContributionPlan> ContributionPlans { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Asset> Assets { get; set; } = [];
    public ICollection<FinancialAccount> FinancialAccounts { get; set; } = [];
}
