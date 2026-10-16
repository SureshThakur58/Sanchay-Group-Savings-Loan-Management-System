using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace GroupFinance.Api.Groups;

public sealed class GroupValidationException(string message) : Exception(message);

public sealed class GroupService(GroupFinanceDbContext db)
{
    private static readonly string[] ContributionFrequencies = ["Monthly", "Quarterly", "Half-Yearly", "Yearly", "Custom"];
    private static readonly string[] LoanMethods = ["Flat", "ReducingBalance"];
    private static readonly string[] DurationUnits = ["Months", "Years"];
    private static readonly string[] InstallmentFrequencies = ["Monthly"];
    private static readonly string[] DistributionMethods = ["Equal", "ContributionBased", "Weighted"];

    public async Task<IReadOnlyCollection<GroupResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await db.Groups.AsNoTracking().Include(x => x.Setting).OrderBy(x => x.Id).Select(MapExpression()).ToArrayAsync(cancellationToken);
    }

    public async Task<GroupResponse?> GetAsync(long id, CancellationToken cancellationToken)
    {
        return await db.Groups.AsNoTracking().Include(x => x.Setting).Where(x => x.Id == id).Select(MapExpression()).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<GroupResponse> CreateAsync(CreateGroupRequest request, long userId, CancellationToken cancellationToken)
    {
        ValidateGroup(request.Code, request.Name, request.FormationDate, request.CurrencyCode);
        ValidateSettings(request.Settings);

        if (await db.Groups.AnyAsync(cancellationToken))
            throw new GroupValidationException("Only one group can be configured in this system. The group already exists.");

        var now = DateTime.UtcNow;
        var group = new Group
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            FormationDate = request.FormationDate.Date,
            RegistrationNumber = Normalize(request.RegistrationNumber),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            IsActive = true,
            SingleGroupKey = 1,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
            Setting = BuildSettings(request.Settings, userId, now)
        };

        db.Groups.Add(group);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            throw new GroupValidationException("Only one group can be configured in this system. The group already exists.");
        }

        return await GetAsync(group.Id, cancellationToken) ?? throw new InvalidOperationException("The group was created but could not be loaded.");
    }

    public async Task<GroupResponse?> UpdateAsync(long id, UpdateGroupRequest request, long userId, CancellationToken cancellationToken)
    {
        ValidateGroup(request.Code, request.Name, request.FormationDate, request.CurrencyCode);

        var group = await db.Groups.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null) return null;

        var code = request.Code.Trim();
        if (await db.Groups.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken))
            throw new GroupValidationException("Another group already uses this group code.");

        group.Code = code;
        group.Name = request.Name.Trim();
        group.Description = Normalize(request.Description);
        group.FormationDate = request.FormationDate.Date;
        group.RegistrationNumber = Normalize(request.RegistrationNumber);
        group.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        group.IsActive = request.IsActive;
        group.ModifiedAtUtc = DateTime.UtcNow;
        group.ModifiedByUserId = userId;

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<GroupSettingsResponse?> GetSettingsAsync(long groupId, CancellationToken cancellationToken)
    {
        return await db.GroupSettings.AsNoTracking().Where(x => x.GroupId == groupId).Select(MapSettingsExpression()).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<GroupSettingsResponse?> UpdateSettingsAsync(long groupId, GroupSettingsRequest request, long userId, CancellationToken cancellationToken)
    {
        ValidateSettings(request);

        if (!await db.Groups.AnyAsync(x => x.Id == groupId, cancellationToken)) return null;
        var setting = await db.GroupSettings.SingleOrDefaultAsync(x => x.GroupId == groupId, cancellationToken);
        if (setting is null) return null;

        ApplySettings(setting, request);
        setting.ModifiedAtUtc = DateTime.UtcNow;
        setting.ModifiedByUserId = userId;
        await db.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(groupId, cancellationToken);
    }

    private static GroupSetting BuildSettings(GroupSettingsRequest request, long userId, DateTime now)
    {
        var setting = new GroupSetting { CreatedAtUtc = now, CreatedByUserId = userId };
        ApplySettings(setting, request);
        return setting;
    }

    private static void ApplySettings(GroupSetting setting, GroupSettingsRequest request)
    {
        setting.DefaultContributionAmount = request.DefaultContributionAmount;
        setting.ContributionFrequency = request.ContributionFrequency.Trim();
        setting.DefaultLoanInterestRate = request.DefaultLoanInterestRate;
        setting.LoanInterestMethod = request.LoanInterestMethod.Trim();
        setting.DefaultLoanDuration = request.DefaultLoanDuration;
        setting.LoanDurationUnit = request.LoanDurationUnit.Trim();
        setting.LoanInstallmentFrequency = request.LoanInstallmentFrequency.Trim();
        setting.ContributionLateFeeRate = request.ContributionLateFeeRate;
        setting.ProfitReserveRate = request.ProfitReserveRate;
        setting.ProfitDistributionMethod = request.ProfitDistributionMethod.Trim();
        setting.LoanContributionMultiplier = request.LoanContributionMultiplier;
        setting.MinimumMembershipMonthsForLoan = request.MinimumMembershipMonthsForLoan;
        setting.MaximumActiveLoansPerMember = request.MaximumActiveLoansPerMember;
        setting.RequiredGuarantors = request.RequiredGuarantors;
        setting.MonetaryPrecision = request.MonetaryPrecision;
        setting.DateFormat = request.DateFormat.Trim();
        setting.AllowBackdatedTransactions = request.AllowBackdatedTransactions;
        setting.AllowFutureFinancialPosting = request.AllowFutureFinancialPosting;
    }

    private static void ValidateGroup(string code, string name, DateTime formationDate, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new GroupValidationException("Group code is required.");
        if (code.Trim().Length > 30) throw new GroupValidationException("Group code cannot exceed 30 characters.");
        if (string.IsNullOrWhiteSpace(name)) throw new GroupValidationException("Group name is required.");
        if (name.Trim().Length > 200) throw new GroupValidationException("Group name cannot exceed 200 characters.");
        if (formationDate == default) throw new GroupValidationException("Formation date is required.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3) throw new GroupValidationException("Currency code must be a 3-letter code.");
    }

    private static void ValidateSettings(GroupSettingsRequest request)
    {
        if (request.DefaultContributionAmount < 0m) throw new GroupValidationException("Default contribution amount cannot be negative.");
        if (!Contains(ContributionFrequencies, request.ContributionFrequency)) throw new GroupValidationException("Unsupported contribution frequency.");
        ValidateRate(request.DefaultLoanInterestRate, "Default loan interest rate");
        if (!Contains(LoanMethods, request.LoanInterestMethod)) throw new GroupValidationException("Unsupported loan interest method.");
        if (request.DefaultLoanDuration <= 0m) throw new GroupValidationException("Default loan duration must be greater than zero.");
        if (!Contains(DurationUnits, request.LoanDurationUnit)) throw new GroupValidationException("Unsupported loan duration unit.");
        if (!Contains(InstallmentFrequencies, request.LoanInstallmentFrequency)) throw new GroupValidationException("Unsupported loan installment frequency.");
        ValidateRate(request.ContributionLateFeeRate, "Contribution late fee rate");
        ValidateRate(request.ProfitReserveRate, "Profit reserve rate");
        if (!Contains(DistributionMethods, request.ProfitDistributionMethod)) throw new GroupValidationException("Unsupported profit distribution method.");
        if (request.LoanContributionMultiplier <= 0m) throw new GroupValidationException("Loan contribution multiplier must be greater than zero.");
        if (request.MinimumMembershipMonthsForLoan < 0) throw new GroupValidationException("Minimum membership months cannot be negative.");
        if (request.MaximumActiveLoansPerMember < 1) throw new GroupValidationException("Maximum active loans per member must be at least one.");
        if (request.RequiredGuarantors < 0) throw new GroupValidationException("Required guarantors cannot be negative.");
        if (request.MonetaryPrecision < 0 || request.MonetaryPrecision > 6) throw new GroupValidationException("Monetary precision must be between 0 and 6.");
        if (string.IsNullOrWhiteSpace(request.DateFormat)) throw new GroupValidationException("Date format is required.");
    }

    private static bool Contains(IEnumerable<string> values, string? value) => values.Contains(value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static void ValidateRate(decimal value, string label)
    {
        if (value < 0m || value > 100m) throw new GroupValidationException($"{label} must be between 0 and 100 percent.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static System.Linq.Expressions.Expression<Func<Group, GroupResponse>> MapExpression() =>
        x => new GroupResponse(
            x.Id, x.Code, x.Name, x.Description, x.FormationDate, x.RegistrationNumber, x.CurrencyCode, x.IsActive,
            x.CreatedAtUtc, x.CreatedByUserId, x.ModifiedAtUtc, x.ModifiedByUserId,
            x.Setting == null ? null : new GroupSettingsResponse(
                x.Setting.Id, x.Setting.GroupId, x.Setting.DefaultContributionAmount, x.Setting.ContributionFrequency,
                x.Setting.DefaultLoanInterestRate, x.Setting.LoanInterestMethod, x.Setting.DefaultLoanDuration,
                x.Setting.LoanDurationUnit, x.Setting.LoanInstallmentFrequency, x.Setting.ContributionLateFeeRate,
                x.Setting.ProfitReserveRate, x.Setting.ProfitDistributionMethod, x.Setting.LoanContributionMultiplier,
                x.Setting.MinimumMembershipMonthsForLoan, x.Setting.MaximumActiveLoansPerMember, x.Setting.RequiredGuarantors,
                x.Setting.MonetaryPrecision, x.Setting.DateFormat, x.Setting.AllowBackdatedTransactions,
                x.Setting.AllowFutureFinancialPosting, x.Setting.CreatedAtUtc, x.Setting.CreatedByUserId,
                x.Setting.ModifiedAtUtc, x.Setting.ModifiedByUserId));

    private static System.Linq.Expressions.Expression<Func<GroupSetting, GroupSettingsResponse>> MapSettingsExpression() =>
        x => new GroupSettingsResponse(
            x.Id, x.GroupId, x.DefaultContributionAmount, x.ContributionFrequency, x.DefaultLoanInterestRate,
            x.LoanInterestMethod, x.DefaultLoanDuration, x.LoanDurationUnit, x.LoanInstallmentFrequency,
            x.ContributionLateFeeRate, x.ProfitReserveRate, x.ProfitDistributionMethod, x.LoanContributionMultiplier,
            x.MinimumMembershipMonthsForLoan, x.MaximumActiveLoansPerMember, x.RequiredGuarantors, x.MonetaryPrecision,
            x.DateFormat, x.AllowBackdatedTransactions, x.AllowFutureFinancialPosting, x.CreatedAtUtc, x.CreatedByUserId,
            x.ModifiedAtUtc, x.ModifiedByUserId);
}
