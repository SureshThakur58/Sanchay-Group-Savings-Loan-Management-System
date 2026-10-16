namespace GroupFinance.Core.Entities;

public sealed class GroupSetting : BaseEntity
{
    public long GroupId { get; set; }
    public decimal DefaultContributionAmount { get; set; }
    public string ContributionFrequency { get; set; } = "Quarterly";
    public decimal DefaultLoanInterestRate { get; set; } = 12m;
    public string LoanInterestMethod { get; set; } = "ReducingBalance";
    public int DefaultLoanDuration { get; set; } = 12;
    public string LoanDurationUnit { get; set; } = "Months";
    public string LoanInstallmentFrequency { get; set; } = "Monthly";
    public decimal ContributionLateFeeRate { get; set; } = 1m;
    public decimal ProfitReserveRate { get; set; } = 10m;
    public string ProfitDistributionMethod { get; set; } = "ContributionBased";
    public decimal LoanContributionMultiplier { get; set; } = 3m;
    public int MinimumMembershipMonthsForLoan { get; set; } = 6;
    public int MaximumActiveLoansPerMember { get; set; } = 1;
    public int RequiredGuarantors { get; set; } = 2;
    public int MonetaryPrecision { get; set; } = 2;
    public string DateFormat { get; set; } = "DD/MM/YYYY";
    public bool AllowBackdatedTransactions { get; set; } = true;
    public bool AllowFutureFinancialPosting { get; set; }

    public Group Group { get; set; } = null!;
}
