using GroupFinance.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroupFinance.Infrastructure.Persistence.Configurations;

internal static class EntityConfigurationHelpers
{
    public static void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> b) where TEntity : BaseEntity
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAtUtc).HasPrecision(0).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CreatedByUserId);
        b.Property(x => x.ModifiedAtUtc).HasPrecision(0);
        b.Property(x => x.ModifiedByUserId);
    }

    public static void Money<T>(PropertyBuilder<T> property)
        => property.HasPrecision(18, 2);
}

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Groups", t => t.HasCheckConstraint("CK_Groups_SingleGroupKey", "[SingleGroupKey] = 1"));
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.FormationDate).HasPrecision(0);
        b.Property(x => x.RegistrationNumber).HasMaxLength(100);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        b.HasIndex(x => x.SingleGroupKey).IsUnique();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class GroupSettingConfiguration : IEntityTypeConfiguration<GroupSetting>
{
    public void Configure(EntityTypeBuilder<GroupSetting> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("GroupSettings");
        EntityConfigurationHelpers.Money(b.Property(x => x.DefaultContributionAmount));
        b.Property(x => x.DefaultLoanInterestRate).HasPrecision(9, 4);
        b.Property(x => x.DefaultLoanDuration);
        b.Property(x => x.ContributionLateFeeRate).HasPrecision(9, 4);
        b.Property(x => x.ProfitReserveRate).HasPrecision(9, 4);
        b.Property(x => x.LoanContributionMultiplier).HasPrecision(9, 4);
        b.Property(x => x.ContributionFrequency).HasMaxLength(30).IsRequired();
        b.Property(x => x.LoanInterestMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.LoanDurationUnit).HasMaxLength(20).IsRequired();
        b.Property(x => x.LoanInstallmentFrequency).HasMaxLength(20).IsRequired();
        b.Property(x => x.ProfitDistributionMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.DateFormat).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.GroupId).IsUnique();
        b.HasOne(x => x.Group).WithOne(x => x.Setting).HasForeignKey<GroupSetting>(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Members");
        b.Property(x => x.MemberNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.CitizenshipNumber).HasMaxLength(50);
        b.Property(x => x.MobileNumber).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);
        EntityConfigurationHelpers.Money(b.Property(x => x.ContributionAmountOverride));
        b.HasIndex(x => new { x.GroupId, x.MemberNumber }).IsUnique();
        b.HasIndex(x => x.CitizenshipNumber);
        b.HasIndex(x => x.MobileNumber);
        b.HasIndex(x => x.Email);
        b.HasOne(x => x.Group).WithMany(x => x.Members).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MemberNomineeConfiguration : IEntityTypeConfiguration<MemberNominee>
{
    public void Configure(EntityTypeBuilder<MemberNominee> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("MemberNominees");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Relationship).HasMaxLength(100);
        b.Property(x => x.MobileNumber).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.SharePercentage).HasPrecision(9, 4);
        b.HasIndex(x => x.MemberId);
        b.HasOne(x => x.Member).WithMany(x => x.Nominees).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContributionPlanConfiguration : IEntityTypeConfiguration<ContributionPlan>
{
    public void Configure(EntityTypeBuilder<ContributionPlan> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("ContributionPlans");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        EntityConfigurationHelpers.Money(b.Property(x => x.Amount));
        b.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
        b.Property(x => x.IntervalMonths);
        b.Property(x => x.LateFeeRate).HasPrecision(9, 4);
        b.HasIndex(x => new { x.GroupId, x.Name }).IsUnique();
        b.HasOne(x => x.Group).WithMany(x => x.ContributionPlans).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContributionScheduleConfiguration : IEntityTypeConfiguration<ContributionSchedule>
{
    public void Configure(EntityTypeBuilder<ContributionSchedule> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("ContributionSchedules");
        EntityConfigurationHelpers.Money(b.Property(x => x.DueAmount));
        EntityConfigurationHelpers.Money(b.Property(x => x.PaidAmount));
        EntityConfigurationHelpers.Money(b.Property(x => x.LateFeeAmount));
        EntityConfigurationHelpers.Money(b.Property(x => x.WaivedLateFeeAmount));
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.ContributionPlanId, x.MemberId, x.PeriodStart }).IsUnique();
        b.HasIndex(x => new { x.MemberId, x.DueDate });
        b.HasOne(x => x.ContributionPlan).WithMany(x => x.Schedules).HasForeignKey(x => x.ContributionPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContributionPaymentConfiguration : IEntityTypeConfiguration<ContributionPayment>
{
    public void Configure(EntityTypeBuilder<ContributionPayment> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("ContributionPayments");
        EntityConfigurationHelpers.Money(b.Property(x => x.Amount));
        b.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.ContributionScheduleId, x.ReferenceNumber })
            .IsUnique()
            .HasFilter("[ReferenceNumber] IS NOT NULL AND [Status] = 'Posted'");
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.TransactionDate);
        b.HasOne(x => x.ContributionSchedule).WithMany(x => x.Payments).HasForeignKey(x => x.ContributionScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Member).WithMany(x => x.ContributionPayments).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FinancialTransaction).WithMany(x => x.ContributionPayments).HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("LoanApplications");
        b.Property(x => x.LoanApplicationNumber).HasMaxLength(30).IsRequired();
        EntityConfigurationHelpers.Money(b.Property(x => x.RequestedAmount));
        b.Property(x => x.ProposedInterestRate).HasPrecision(9, 4);
        b.Property(x => x.ProposedInterestMethod).HasMaxLength(30).IsRequired();
        EntityConfigurationHelpers.Money(b.Property(x => x.RequestedEmi));
        b.Property(x => x.Purpose).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.DecisionRemarks).HasMaxLength(1000);
        b.HasIndex(x => new { x.MemberId, x.ApplicationDate });
        b.HasIndex(x => x.LoanApplicationNumber).IsUnique();
        b.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Loans");
        b.Property(x => x.LoanNumber).HasMaxLength(30).IsRequired();
        EntityConfigurationHelpers.Money(b.Property(x => x.PrincipalAmount));
        b.Property(x => x.InterestRate).HasPrecision(9, 4);
        EntityConfigurationHelpers.Money(b.Property(x => x.PrincipalPaid));
        EntityConfigurationHelpers.Money(b.Property(x => x.InterestPaid));
        EntityConfigurationHelpers.Money(b.Property(x => x.PenaltyPaid));
        EntityConfigurationHelpers.Money(b.Property(x => x.OverpaymentAmount));
        b.Property(x => x.InterestMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.InstallmentFrequency).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.GroupId, x.LoanNumber }).IsUnique();
        b.HasIndex(x => new { x.MemberId, x.Status });
        b.HasOne(x => x.Group).WithMany(x => x.Loans).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Member).WithMany(x => x.Loans).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoanApplication).WithOne(x => x.Loan).HasForeignKey<Loan>(x => x.LoanApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoanGuarantorConfiguration : IEntityTypeConfiguration<LoanGuarantor>
{
    public void Configure(EntityTypeBuilder<LoanGuarantor> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("LoanGuarantors");
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.LoanId, x.MemberId }).IsUnique().HasFilter("[LoanId] IS NOT NULL AND [Status] = 'Active'");
        b.HasIndex(x => new { x.LoanApplicationId, x.MemberId }).IsUnique().HasFilter("[LoanApplicationId] IS NOT NULL AND [Status] = 'Active'");
        b.HasOne(x => x.Loan).WithMany(x => x.Guarantors).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Member).WithMany(x => x.GuaranteedLoans).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoanApplication).WithMany(x => x.Guarantors).HasForeignKey(x => x.LoanApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoanScheduleConfiguration : IEntityTypeConfiguration<LoanSchedule>
{
    public void Configure(EntityTypeBuilder<LoanSchedule> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("LoanSchedules");
        EntityConfigurationHelpers.Money(b.Property(x => x.OpeningPrincipal));
        EntityConfigurationHelpers.Money(b.Property(x => x.PrincipalDue));
        EntityConfigurationHelpers.Money(b.Property(x => x.InterestDue));
        EntityConfigurationHelpers.Money(b.Property(x => x.PenaltyDue));
        EntityConfigurationHelpers.Money(b.Property(x => x.TotalDue));
        EntityConfigurationHelpers.Money(b.Property(x => x.PrincipalPaid));
        EntityConfigurationHelpers.Money(b.Property(x => x.InterestPaid));
        EntityConfigurationHelpers.Money(b.Property(x => x.PenaltyPaid));
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.LoanId, x.InstallmentNumber }).IsUnique();
        b.HasIndex(x => new { x.LoanId, x.DueDate });
        b.HasOne(x => x.Loan).WithMany(x => x.Schedules).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LoanPaymentConfiguration : IEntityTypeConfiguration<LoanPayment>
{
    public void Configure(EntityTypeBuilder<LoanPayment> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("LoanPayments");
        EntityConfigurationHelpers.Money(b.Property(x => x.Amount));
        EntityConfigurationHelpers.Money(b.Property(x => x.PenaltyAllocated));
        EntityConfigurationHelpers.Money(b.Property(x => x.InterestAllocated));
        EntityConfigurationHelpers.Money(b.Property(x => x.PrincipalAllocated));
        EntityConfigurationHelpers.Money(b.Property(x => x.OverpaymentAmount));
        b.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.TransactionDate);
        b.HasIndex(x => new { x.LoanId, x.ReferenceNumber })
            .IsUnique()
            .HasFilter("[ReferenceNumber] IS NOT NULL AND [Status] = 'Posted'");
        b.HasOne(x => x.Loan).WithMany(x => x.Payments).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoanSchedule).WithMany(x => x.Payments).HasForeignKey(x => x.LoanScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FinancialTransaction).WithMany(x => x.LoanPayments).HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("FinancialAccounts");
        b.Property(x => x.AccountCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.AccountName).HasMaxLength(200).IsRequired();
        b.Property(x => x.AccountType).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.GroupId, x.AccountCode }).IsUnique();
        b.HasOne(x => x.Group).WithMany(x => x.FinancialAccounts).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("FinancialTransactions", t =>
        {
            t.HasCheckConstraint("CK_FinancialTransactions_Balanced", "[TotalDebit] = [TotalCredit]");
            t.HasCheckConstraint("CK_FinancialTransactions_AmountPositive", "[Amount] > 0 AND [TotalDebit] > 0 AND [TotalCredit] > 0");
        });
        b.Property(x => x.TransactionNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.TransactionType).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.TotalDebit).HasPrecision(18, 2);
        b.Property(x => x.TotalCredit).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.ReferenceType).HasMaxLength(100);
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.HasIndex(x => x.TransactionNumber).IsUnique();
        b.HasIndex(x => new { x.GroupId, x.TransactionDate, x.TransactionType });
        b.HasIndex(x => new { x.GroupId, x.MemberId, x.TransactionDate });
        b.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReversalOfTransaction).WithOne(x => x.ReversedByTransaction).HasForeignKey<FinancialTransaction>(x => x.ReversalOfTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionDetailConfiguration : IEntityTypeConfiguration<TransactionDetail>
{
    public void Configure(EntityTypeBuilder<TransactionDetail> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("TransactionDetails", t =>
            t.HasCheckConstraint("CK_TransactionDetails_DebitCredit", "([DebitAmount] > 0 AND [CreditAmount] = 0) OR ([CreditAmount] > 0 AND [DebitAmount] = 0)"));
        EntityConfigurationHelpers.Money(b.Property(x => x.DebitAmount));
        EntityConfigurationHelpers.Money(b.Property(x => x.CreditAmount));
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.FinancialTransactionId);
        b.HasIndex(x => x.FinancialAccountId);
        b.HasOne(x => x.FinancialTransaction).WithMany(x => x.Details).HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.FinancialAccount).WithMany(x => x.TransactionDetails).HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InterestCalculationConfiguration : IEntityTypeConfiguration<InterestCalculation>
{
    public void Configure(EntityTypeBuilder<InterestCalculation> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("InterestCalculations");
        EntityConfigurationHelpers.Money(b.Property(x => x.OpeningPrincipal));
        b.Property(x => x.InterestRate).HasPrecision(9, 4);
        EntityConfigurationHelpers.Money(b.Property(x => x.InterestAmount));
        b.Property(x => x.CalculationMethod).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.LoanId, x.PeriodStart, x.PeriodEnd }).IsUnique();
        b.HasOne(x => x.Loan).WithMany(x => x.InterestCalculations).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProfitDistributionConfiguration : IEntityTypeConfiguration<ProfitDistribution>
{
    public void Configure(EntityTypeBuilder<ProfitDistribution> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("ProfitDistributions");
        EntityConfigurationHelpers.Money(b.Property(x => x.NetProfit));
        EntityConfigurationHelpers.Money(b.Property(x => x.ReserveAmount));
        EntityConfigurationHelpers.Money(b.Property(x => x.DistributableAmount));
        b.Property(x => x.DistributionMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.GroupId, x.PeriodStart, x.PeriodEnd }).IsUnique();
        b.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProfitDistributionDetailConfiguration : IEntityTypeConfiguration<ProfitDistributionDetail>
{
    public void Configure(EntityTypeBuilder<ProfitDistributionDetail> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("ProfitDistributionDetails");
        b.Property(x => x.EligibilityFactor).HasPrecision(18, 8);
        EntityConfigurationHelpers.Money(b.Property(x => x.CalculatedShare));
        EntityConfigurationHelpers.Money(b.Property(x => x.DistributedAmount));
        b.HasIndex(x => new { x.ProfitDistributionId, x.MemberId }).IsUnique();
        b.HasOne(x => x.ProfitDistribution).WithMany(x => x.Details).HasForeignKey(x => x.ProfitDistributionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("ExpenseCategories");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.GroupId, x.Name }).IsUnique();
        b.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Expenses");
        b.Property(x => x.ExpenseNumber).HasMaxLength(30).IsRequired();
        EntityConfigurationHelpers.Money(b.Property(x => x.Amount));
        b.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.GroupId, x.ExpenseNumber }).IsUnique();
        b.HasIndex(x => x.ExpenseDate);
        b.HasOne(x => x.Group).WithMany(x => x.Expenses).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ExpenseCategory).WithMany(x => x.Expenses).HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FinancialTransaction).WithMany().HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Assets");
        b.Property(x => x.AssetCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.AssetName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasMaxLength(50).IsRequired();
        b.Property(x => x.Location).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(1000);
        EntityConfigurationHelpers.Money(b.Property(x => x.AcquisitionCost));
        EntityConfigurationHelpers.Money(b.Property(x => x.BookValue));
        b.HasIndex(x => new { x.GroupId, x.AssetCode }).IsUnique();
        b.HasOne(x => x.Group).WithMany(x => x.Assets).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssetTransactionConfiguration : IEntityTypeConfiguration<AssetTransaction>
{
    public void Configure(EntityTypeBuilder<AssetTransaction> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("AssetTransactions");
        b.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        EntityConfigurationHelpers.Money(b.Property(x => x.Amount));
        EntityConfigurationHelpers.Money(b.Property(x => x.BookValueAtSale));
        EntityConfigurationHelpers.Money(b.Property(x => x.GainLoss));
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.AssetId, x.TransactionDate });
        b.HasOne(x => x.Asset).WithMany(x => x.Transactions).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.FinancialTransaction).WithMany().HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Notifications");
        b.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("AuditLogs");
        b.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Action).HasMaxLength(50).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("Attachments");
        b.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        b.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        EntityConfigurationHelpers.ConfigureBase(b);
        b.ToTable("SystemSettings");
        b.Property(x => x.SettingKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.SettingValue).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.SettingKey).IsUnique();
    }
}
