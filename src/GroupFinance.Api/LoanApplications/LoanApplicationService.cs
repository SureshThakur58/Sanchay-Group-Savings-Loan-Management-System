using GroupFinance.Core.Entities;
using GroupFinance.Core.Loans;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.LoanApplications;

public sealed class LoanApplicationValidationException(string message) : Exception(message);

public sealed class LoanApplicationService(GroupFinanceDbContext db, ILoanCalculationService calculationEngine)
{
    public async Task<IReadOnlyCollection<LoanApplicationSummaryResponse>> ListAsync(long groupId, string? status, long? memberId, CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var query = db.LoanApplications.AsNoTracking()
            .Include(x => x.Member)
            .Where(x => x.Member.GroupId == groupId);

        if (memberId.HasValue) query = query.Where(x => x.MemberId == memberId.Value);
        var normalizedStatus = status?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
            query = query.Where(x => x.Status == normalizedStatus);

        return await query.OrderByDescending(x => x.ApplicationDate).ThenByDescending(x => x.Id)
            .Select(x => new LoanApplicationSummaryResponse(
                x.Id, groupId, x.MemberId, x.Member.MemberNumber, x.Member.FullName,
                x.LoanApplicationNumber, x.ApplicationDate, x.RequestedAmount,
                x.ProposedInterestRate, x.ProposedInterestMethod, x.RequestedDurationMonths,
                x.RequestedEmi, x.Status, x.Guarantors.Count(g => g.Status == "Active"), x.DecisionDate))
            .ToArrayAsync(ct);
    }

    public async Task<LoanApplicationResponse?> GetAsync(long id, CancellationToken ct)
    {
        var application = await db.LoanApplications.AsNoTracking()
            .Include(x => x.Member).ThenInclude(x => x.Group)
            .Include(x => x.Guarantors).ThenInclude(x => x.Member)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null) return null;
        return await MapAsync(application, ct);
    }

    public async Task<LoanApplicationResponse> CreateAsync(CreateLoanApplicationRequest request, long userId, CancellationToken ct)
    {
        await EnsureGroupAsync(request.GroupId, ct);
        var member = await db.Members.Include(x => x.Group).SingleOrDefaultAsync(x => x.Id == request.MemberId, ct);
        if (member is null || member.GroupId != request.GroupId)
            throw new LoanApplicationValidationException("The selected member does not belong to the configured group.");
        if (!member.IsActive)
            throw new LoanApplicationValidationException("Only active members can create loan applications.");

        var settings = await GetSettingsAsync(request.GroupId, ct);
        var duration = request.RequestedDurationMonths ?? settings.DefaultLoanDuration;
        var rate = request.ProposedInterestRate ?? settings.DefaultLoanInterestRate;
        var method = string.IsNullOrWhiteSpace(request.ProposedInterestMethod) ? settings.LoanInterestMethod : request.ProposedInterestMethod;
        ValidateApplication(request.ApplicationDate, request.RequestedAmount, request.Purpose, duration, rate, method);

        var calculation = CalculateEmi(request.RequestedAmount, rate, duration, method, request.ApplicationDate, settings.MonetaryPrecision);
        var now = DateTime.UtcNow;
        var entity = new LoanApplication
        {
            MemberId = request.MemberId,
            LoanApplicationNumber = $"LA-{Guid.NewGuid():N}"[..27],
            ApplicationDate = request.ApplicationDate.Date,
            RequestedAmount = RoundMoney(request.RequestedAmount, settings.MonetaryPrecision),
            RequestedDurationMonths = duration,
            ProposedInterestRate = rate,
            ProposedInterestMethod = NormalizeMethod(method),
            RequestedEmi = calculation.MonthlyInstallment,
            Purpose = request.Purpose.Trim(),
            Status = LoanApplicationStatuses.Draft,
            CreatedAtUtc = now,
            CreatedByUserId = userId
        };

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.LoanApplications.Add(entity);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return await GetAsync(entity.Id, ct) ?? throw new InvalidOperationException("The loan application was created but could not be loaded.");
    }

    public async Task<LoanApplicationResponse?> UpdateAsync(long id, UpdateLoanApplicationRequest request, long userId, CancellationToken ct)
    {
        var entity = await db.LoanApplications.Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != LoanApplicationStatuses.Draft)
            throw new LoanApplicationValidationException("Only Draft loan applications can be edited.");

        var settings = await GetSettingsAsync(entity.Member.GroupId, ct);
        ValidateApplication(request.ApplicationDate, request.RequestedAmount, request.Purpose, request.RequestedDurationMonths, request.ProposedInterestRate, request.ProposedInterestMethod);
        var calculation = CalculateEmi(request.RequestedAmount, request.ProposedInterestRate, request.RequestedDurationMonths, request.ProposedInterestMethod, request.ApplicationDate, settings.MonetaryPrecision);

        entity.ApplicationDate = request.ApplicationDate.Date;
        entity.RequestedAmount = RoundMoney(request.RequestedAmount, settings.MonetaryPrecision);
        entity.RequestedDurationMonths = request.RequestedDurationMonths;
        entity.ProposedInterestRate = request.ProposedInterestRate;
        entity.ProposedInterestMethod = NormalizeMethod(request.ProposedInterestMethod);
        entity.RequestedEmi = calculation.MonthlyInstallment;
        entity.Purpose = request.Purpose.Trim();
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<LoanApplicationResponse?> SubmitAsync(long id, long userId, CancellationToken ct)
    {
        var entity = await db.LoanApplications.Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != LoanApplicationStatuses.Draft)
            throw new LoanApplicationValidationException("Only Draft loan applications can be submitted.");

        var eligibility = await EvaluateEligibilityAsync(entity, ct);
        var settings = await GetSettingsAsync(entity.Member.GroupId, ct);
        var guarantorCount = await db.LoanGuarantors.CountAsync(x => x.LoanApplicationId == id && x.Status == "Active", ct);
        if (guarantorCount < settings.RequiredGuarantors)
            throw new LoanApplicationValidationException($"At least {settings.RequiredGuarantors} active guarantor(s) are required before submission.");
        if (!eligibility.IsEligible)
            throw new LoanApplicationValidationException(string.Join(" ", eligibility.Reasons));

        entity.Status = LoanApplicationStatuses.Submitted;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<LoanApplicationResponse?> ApproveAsync(long id, string? remarks, long userId, CancellationToken ct)
        => await DecideAsync(id, LoanApplicationStatuses.Approved, remarks, userId, ct);

    public async Task<LoanApplicationResponse?> RejectAsync(long id, string? remarks, long userId, CancellationToken ct)
        => await DecideAsync(id, LoanApplicationStatuses.Rejected, remarks, userId, ct);

    public async Task<LoanApplicationResponse?> CancelAsync(long id, long userId, CancellationToken ct)
    {
        var entity = await db.LoanApplications.Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status is not LoanApplicationStatuses.Draft and not LoanApplicationStatuses.Submitted)
            throw new LoanApplicationValidationException("Only Draft or Submitted loan applications can be cancelled.");
        entity.Status = LoanApplicationStatuses.Cancelled;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyCollection<LoanApplicationGuarantorResponse>?> ListGuarantorsAsync(long id, CancellationToken ct)
    {
        if (!await db.LoanApplications.AnyAsync(x => x.Id == id, ct)) return null;
        return await db.LoanGuarantors.AsNoTracking().Include(x => x.Member)
            .Where(x => x.LoanApplicationId == id)
            .OrderBy(x => x.Id)
            .Select(x => new LoanApplicationGuarantorResponse(x.Id, id, x.MemberId, x.Member.MemberNumber, x.Member.FullName, x.Status, x.ProvidedAt, x.CreatedAtUtc, x.CreatedByUserId))
            .ToArrayAsync(ct);
    }

    public async Task<LoanApplicationGuarantorResponse> AddGuarantorAsync(long id, AddLoanApplicationGuarantorRequest request, long userId, CancellationToken ct)
    {
        var application = await db.LoanApplications.Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null) throw new LoanApplicationValidationException("Loan application not found.");
        if (application.Status != LoanApplicationStatuses.Draft)
            throw new LoanApplicationValidationException("Guarantors can only be changed while the application is Draft.");
        if (request.MemberId == application.MemberId)
            throw new LoanApplicationValidationException("The applicant cannot be their own guarantor.");

        var guarantor = await db.Members.SingleOrDefaultAsync(x => x.Id == request.MemberId, ct);
        if (guarantor is null || guarantor.GroupId != application.Member.GroupId)
            throw new LoanApplicationValidationException("The guarantor must be a member of the configured group.");
        if (!guarantor.IsActive)
            throw new LoanApplicationValidationException("Only active members can be guarantors.");
        if (await db.LoanGuarantors.AnyAsync(x => x.LoanApplicationId == id && x.MemberId == request.MemberId && x.Status == "Active", ct))
            throw new LoanApplicationValidationException("This member is already an active guarantor for the application.");

        var now = DateTime.UtcNow;
        var entity = new LoanGuarantor
        {
            LoanId = null,
            LoanApplicationId = id,
            MemberId = request.MemberId,
            ProvidedAt = now,
            Status = "Active",
            CreatedAtUtc = now,
            CreatedByUserId = userId
        };
        db.LoanGuarantors.Add(entity);
        await db.SaveChangesAsync(ct);
        return new LoanApplicationGuarantorResponse(entity.Id, id, entity.MemberId, guarantor.MemberNumber, guarantor.FullName, entity.Status, entity.ProvidedAt, entity.CreatedAtUtc, entity.CreatedByUserId);
    }

    public async Task<bool> RemoveGuarantorAsync(long id, long guarantorId, long userId, CancellationToken ct)
    {
        var application = await db.LoanApplications.FindAsync([id], ct);
        if (application is null) return false;
        if (application.Status != LoanApplicationStatuses.Draft)
            throw new LoanApplicationValidationException("Guarantors can only be changed while the application is Draft.");
        var guarantor = await db.LoanGuarantors.SingleOrDefaultAsync(x => x.Id == guarantorId && x.LoanApplicationId == id, ct);
        if (guarantor is null) return false;
        guarantor.Status = "Inactive";
        guarantor.ModifiedAtUtc = DateTime.UtcNow;
        guarantor.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<LoanApplicationResponse?> DecideAsync(long id, string decision, string? remarks, long userId, CancellationToken ct)
    {
        var entity = await db.LoanApplications.Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != LoanApplicationStatuses.Submitted)
            throw new LoanApplicationValidationException("Only Submitted loan applications can be approved or rejected.");

        if (decision == LoanApplicationStatuses.Approved)
        {
            var eligibility = await EvaluateEligibilityAsync(entity, ct);
            if (!eligibility.IsEligible)
                throw new LoanApplicationValidationException("The application is no longer eligible for approval. " + string.Join(" ", eligibility.Reasons));
            var settings = await GetSettingsAsync(entity.Member.GroupId, ct);
            var guarantorCount = await db.LoanGuarantors.CountAsync(x => x.LoanApplicationId == id && x.Status == "Active", ct);
            if (guarantorCount < settings.RequiredGuarantors)
                throw new LoanApplicationValidationException($"At least {settings.RequiredGuarantors} active guarantor(s) are required for approval.");
        }

        entity.Status = decision;
        entity.DecisionDate = DateTime.UtcNow;
        entity.DecidedByUserId = userId;
        entity.DecisionRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<LoanEligibilityResponse> EvaluateEligibilityAsync(LoanApplication entity, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleAsync(x => x.Id == entity.MemberId, ct);
        var settings = await GetSettingsAsync(member.GroupId, ct);
        var contribution = await db.ContributionPayments.AsNoTracking()
            .Where(x => x.MemberId == member.Id && x.Status == "Posted")
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var activeLoanCount = await db.Loans.AsNoTracking()
            .Where(x => x.MemberId == member.Id && x.Status != LoanApplicationStatuses.Closed && x.Status != LoanApplicationStatuses.Cancelled)
            .CountAsync(ct);
        var result = LoanEligibilityEvaluator.Evaluate(new LoanEligibilityInput(
            member.JoinDate, entity.ApplicationDate, member.IsActive, contribution, entity.RequestedAmount,
            activeLoanCount, settings.MinimumMembershipMonthsForLoan, settings.LoanContributionMultiplier, settings.MaximumActiveLoansPerMember));
        return new LoanEligibilityResponse(result.IsEligible, result.MembershipMonths, RoundMoney(contribution, settings.MonetaryPrecision), RoundMoney(result.MaximumEligibleAmount, settings.MonetaryPrecision), result.ActiveLoanCount, settings.MaximumActiveLoansPerMember, settings.MinimumMembershipMonthsForLoan, settings.LoanContributionMultiplier, result.Reasons);
    }

    private async Task<LoanApplicationResponse> MapAsync(LoanApplication application, CancellationToken ct)
    {
        var eligibility = await EvaluateEligibilityAsync(application, ct);
        var guarantors = application.Guarantors.Select(x => new LoanApplicationGuarantorResponse(x.Id, application.Id, x.MemberId, x.Member.MemberNumber, x.Member.FullName, x.Status, x.ProvidedAt, x.CreatedAtUtc, x.CreatedByUserId)).ToArray();
        return new LoanApplicationResponse(
            application.Id, application.Member.GroupId, application.Member.Group.Code, application.Member.Group.Name,
            application.MemberId, application.Member.MemberNumber, application.Member.FullName, application.LoanApplicationNumber,
            application.ApplicationDate, application.RequestedAmount, application.Purpose, application.ProposedInterestRate,
            application.ProposedInterestMethod, application.RequestedDurationMonths, application.RequestedEmi, application.Status,
            application.DecisionDate, application.DecidedByUserId, application.DecisionRemarks, guarantors, eligibility,
            application.CreatedAtUtc, application.CreatedByUserId, application.ModifiedAtUtc, application.ModifiedByUserId);
    }

    private async Task<GroupSetting> GetSettingsAsync(long groupId, CancellationToken ct)
        => await db.GroupSettings.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == groupId, ct)
           ?? throw new LoanApplicationValidationException("The configured group does not have loan settings.");

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (groupId <= 0 || !await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new LoanApplicationValidationException("The configured group was not found or is inactive.");
    }

    private static void ValidateApplication(DateTime applicationDate, decimal amount, string purpose, int duration, decimal rate, string method)
    {
        if (applicationDate == default) throw new LoanApplicationValidationException("Application date is required.");
        if (amount <= 0m) throw new LoanApplicationValidationException("Requested amount must be greater than zero.");
        if (duration <= 0) throw new LoanApplicationValidationException("Requested duration must be greater than zero.");
        if (rate < 0m || rate > 100m) throw new LoanApplicationValidationException("Proposed interest rate must be between 0 and 100 percent.");
        if (!LoanInterestMethods.IsSupported(method)) throw new LoanApplicationValidationException("Unsupported loan interest method. Use Flat or ReducingBalance.");
        if (string.IsNullOrWhiteSpace(purpose)) throw new LoanApplicationValidationException("Loan purpose is required.");
        if (purpose.Trim().Length > 1000) throw new LoanApplicationValidationException("Loan purpose cannot exceed 1000 characters.");
    }

    private LoanCalculationResult CalculateEmi(decimal amount, decimal rate, int duration, string method, DateTime applicationDate, int precision)
        => calculationEngine.Calculate(new GroupFinance.Core.Loans.LoanCalculationRequest(amount, rate, duration, method, applicationDate.Date.AddMonths(1), precision));

    private static decimal RoundMoney(decimal value, int precision) => Math.Round(value, precision, MidpointRounding.AwayFromZero);
    private static string NormalizeMethod(string method) => method.Equals("Flat", StringComparison.OrdinalIgnoreCase) ? LoanInterestMethods.Flat : LoanInterestMethods.ReducingBalance;
}
