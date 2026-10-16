namespace GroupFinance.Core.Entities;

public sealed class Member : BaseEntity
{
    public long GroupId { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CitizenshipNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime JoinDate { get; set; }
    public DateTime? ExitDate { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? ContributionAmountOverride { get; set; }
    public string? Notes { get; set; }

    public Group Group { get; set; } = null!;
    public ICollection<MemberNominee> Nominees { get; set; } = [];
    public ICollection<ContributionPayment> ContributionPayments { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
    public ICollection<LoanGuarantor> GuaranteedLoans { get; set; } = [];
}
