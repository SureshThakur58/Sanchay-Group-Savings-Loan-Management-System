using GroupFinance.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Infrastructure.Persistence;

public sealed class GroupFinanceDbContext(DbContextOptions<GroupFinanceDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupSetting> GroupSettings => Set<GroupSetting>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberNominee> MemberNominees => Set<MemberNominee>();
    public DbSet<ContributionPlan> ContributionPlans => Set<ContributionPlan>();
    public DbSet<ContributionSchedule> ContributionSchedules => Set<ContributionSchedule>();
    public DbSet<ContributionPayment> ContributionPayments => Set<ContributionPayment>();
    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanGuarantor> LoanGuarantors => Set<LoanGuarantor>();
    public DbSet<LoanSchedule> LoanSchedules => Set<LoanSchedule>();
    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<TransactionDetail> TransactionDetails => Set<TransactionDetail>();
    public DbSet<InterestCalculation> InterestCalculations => Set<InterestCalculation>();
    public DbSet<ProfitDistribution> ProfitDistributions => Set<ProfitDistribution>();
    public DbSet<ProfitDistributionDetail> ProfitDistributionDetails => Set<ProfitDistributionDetail>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetTransaction> AssetTransactions => Set<AssetTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GroupFinanceDbContext).Assembly);
    }
}
