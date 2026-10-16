using System.Net.Mail;
using System.Data;
using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace GroupFinance.Api.Members;

public sealed class MemberValidationException(string message) : Exception(message);

public sealed class MemberService(GroupFinanceDbContext db)
{
    public async Task<IReadOnlyCollection<MemberResponse>> ListAsync(long groupId, bool includeInactive, CancellationToken cancellationToken)
    {
        await EnsureConfiguredGroupAsync(groupId, cancellationToken);

        var query = db.Members.AsNoTracking()
            .Where(x => x.GroupId == groupId);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var members = await query
            .Include(x => x.Group)
            .Include(x => x.Nominees)
            .OrderBy(x => x.MemberNumber)
            .ToArrayAsync(cancellationToken);

        return members.Select(Map).ToArray();
    }

    public async Task<MemberResponse?> GetAsync(long id, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking()
            .Include(x => x.Group)
            .Include(x => x.Nominees.OrderBy(n => n.Id))
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return member is null ? null : Map(member);
    }

    public async Task<string> GetNextMemberNumberAsync(long groupId, CancellationToken cancellationToken)
    {
        await EnsureConfiguredGroupAsync(groupId, cancellationToken);
        return await GenerateNextMemberNumberAsync(groupId, cancellationToken);
    }

    public async Task<MemberResponse> CreateAsync(CreateMemberRequest request, long userId, CancellationToken cancellationToken)
    {
        await EnsureConfiguredGroupAsync(request.GroupId, cancellationToken);
        ValidateMember(request.FullName, request.Email, request.DateOfBirth, request.JoinDate, null, request.ContributionAmountOverride);
        ValidateNominees(request.Nominees);

        // Member numbers are system-generated. The client-supplied value is intentionally ignored.
        // Serializable isolation prevents two concurrent creates from receiving the same next number.
        // SQL Server retry-on-failure requires the entire user-initiated transaction to execute
        // through EF Core's execution strategy. Keeping the transaction outside ExecuteAsync
        // causes InvalidOperationException before SaveChanges can run.
        var strategy = db.Database.CreateExecutionStrategy();
        long memberId = 0;

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var memberNumber = await GenerateNextMemberNumberAsync(request.GroupId, cancellationToken);
            var now = DateTime.UtcNow;
            var member = new Member
            {
                GroupId = request.GroupId,
                MemberNumber = memberNumber,
                FullName = request.FullName.Trim(),
                Address = Normalize(request.Address),
                MobileNumber = Normalize(request.MobileNumber),
                Email = Normalize(request.Email),
                DateOfBirth = request.DateOfBirth?.Date,
                JoinDate = request.JoinDate.Date,
                CitizenshipNumber = Normalize(request.CitizenshipNumber),
                ContributionAmountOverride = request.ContributionAmountOverride,
                Notes = Normalize(request.Notes),
                IsActive = true,
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };

            if (request.Nominees is not null)
            {
                foreach (var nomineeRequest in request.Nominees)
                {
                    member.Nominees.Add(new MemberNominee
                    {
                        FullName = nomineeRequest.FullName.Trim(),
                        Relationship = Normalize(nomineeRequest.Relationship),
                        MobileNumber = Normalize(nomineeRequest.MobileNumber),
                        Address = Normalize(nomineeRequest.Address),
                        SharePercentage = nomineeRequest.SharePercentage,
                        IsActive = nomineeRequest.IsActive,
                        CreatedAtUtc = now,
                        CreatedByUserId = userId
                    });
                }
            }

            db.Members.Add(member);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                memberId = member.Id;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                throw new MemberValidationException("Another member already uses this member code.");
            }
        });

        return await GetAsync(memberId, cancellationToken)
            ?? throw new InvalidOperationException("The member was created but could not be loaded.");
    }

    public async Task<MemberResponse?> UpdateAsync(long id, UpdateMemberRequest request, long userId, CancellationToken cancellationToken)
    {
        ValidateMember(request.FullName, request.Email, request.DateOfBirth, request.JoinDate, request.ExitDate, request.ContributionAmountOverride);
        var member = await db.Members.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (member is null) return null;

        await EnsureConfiguredGroupAsync(member.GroupId, cancellationToken);

        // Member numbers are immutable after creation. Ignore any client-supplied value.
        member.FullName = request.FullName.Trim();
        member.Address = Normalize(request.Address);
        member.MobileNumber = Normalize(request.MobileNumber);
        member.Email = Normalize(request.Email);
        member.DateOfBirth = request.DateOfBirth?.Date;
        member.JoinDate = request.JoinDate.Date;
        member.ExitDate = request.ExitDate?.Date;
        member.IsActive = request.IsActive;
        member.CitizenshipNumber = Normalize(request.CitizenshipNumber);
        member.ContributionAmountOverride = request.ContributionAmountOverride;
        member.Notes = Normalize(request.Notes);
        member.ModifiedAtUtc = DateTime.UtcNow;
        member.ModifiedByUserId = userId;

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MemberNomineeResponse>?> ListNomineesAsync(long memberId, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(x => x.Id == memberId, cancellationToken);
        if (!memberExists) return null;

        return await db.MemberNominees.AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderBy(x => x.Id)
            .Select(x => new MemberNomineeResponse(
                x.Id, x.MemberId, x.FullName, x.Relationship, x.MobileNumber, x.Address,
                x.SharePercentage, x.IsActive, x.CreatedAtUtc, x.CreatedByUserId, x.ModifiedAtUtc, x.ModifiedByUserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MemberNomineeResponse> AddNomineeAsync(long memberId, CreateMemberNomineeRequest request, long userId, CancellationToken cancellationToken)
    {
        ValidateNominee(request.FullName, request.SharePercentage);
        var member = await db.Members.SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken)
            ?? throw new MemberValidationException("Member not found.");
        await EnsureConfiguredGroupAsync(member.GroupId, cancellationToken);

        var now = DateTime.UtcNow;
        var nominee = new MemberNominee
        {
            MemberId = memberId,
            FullName = request.FullName.Trim(),
            Relationship = Normalize(request.Relationship),
            MobileNumber = Normalize(request.MobileNumber),
            Address = Normalize(request.Address),
            SharePercentage = request.SharePercentage,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            CreatedByUserId = userId
        };

        db.MemberNominees.Add(nominee);
        await db.SaveChangesAsync(cancellationToken);
        return MapNominee(nominee);
    }

    public async Task<MemberNomineeResponse?> UpdateNomineeAsync(long memberId, long nomineeId, UpdateMemberNomineeRequest request, long userId, CancellationToken cancellationToken)
    {
        ValidateNominee(request.FullName, request.SharePercentage);
        var member = await db.Members.SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken);
        if (member is null) return null;
        await EnsureConfiguredGroupAsync(member.GroupId, cancellationToken);

        var nominee = await db.MemberNominees.SingleOrDefaultAsync(x => x.Id == nomineeId && x.MemberId == memberId, cancellationToken);
        if (nominee is null) return null;

        nominee.FullName = request.FullName.Trim();
        nominee.Relationship = Normalize(request.Relationship);
        nominee.MobileNumber = Normalize(request.MobileNumber);
        nominee.Address = Normalize(request.Address);
        nominee.SharePercentage = request.SharePercentage;
        nominee.IsActive = request.IsActive;
        nominee.ModifiedAtUtc = DateTime.UtcNow;
        nominee.ModifiedByUserId = userId;

        await db.SaveChangesAsync(cancellationToken);
        return MapNominee(nominee);
    }

    public async Task<bool?> RemoveNomineeAsync(long memberId, long nomineeId, long userId, CancellationToken cancellationToken)
    {
        var member = await db.Members.SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken);
        if (member is null) return null;
        await EnsureConfiguredGroupAsync(member.GroupId, cancellationToken);

        var nominee = await db.MemberNominees.SingleOrDefaultAsync(x => x.Id == nomineeId && x.MemberId == memberId, cancellationToken);
        if (nominee is null) return false;

        db.MemberNominees.Remove(nominee);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureConfiguredGroupAsync(long groupId, CancellationToken cancellationToken)
    {
        var group = await db.Groups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null)
            throw new MemberValidationException("The selected group does not exist.");
        if (!group.IsActive)
            throw new MemberValidationException("The selected group is inactive.");

        var configuredGroupCount = await db.Groups.CountAsync(cancellationToken);
        if (configuredGroupCount != 1)
            throw new MemberValidationException("Exactly one group must be configured before members can be managed.");
    }

    private async Task<string> GenerateNextMemberNumberAsync(long groupId, CancellationToken cancellationToken)
    {
        var numbers = await db.Members
            .Where(x => x.GroupId == groupId)
            .Select(x => x.MemberNumber)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var number in numbers)
        {
            if (!number.StartsWith("MEM-", StringComparison.OrdinalIgnoreCase))
                continue;

            var suffix = number[4..];
            if (int.TryParse(suffix, out var value) && value > max)
                max = value;
        }

        var next = max + 1;
        if (next > 999999999)
            throw new MemberValidationException("No more member numbers are available.");

        return $"MEM-{next:000}";
    }

    private static void ValidateMember(string fullName, string? email, DateTime? dateOfBirth, DateTime joinDate, DateTime? exitDate, decimal? contributionOverride)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new MemberValidationException("Full name is required.");
        if (fullName.Trim().Length > 200) throw new MemberValidationException("Full name cannot exceed 200 characters.");
        if (joinDate == default) throw new MemberValidationException("Joining date is required.");
        if (dateOfBirth.HasValue && dateOfBirth.Value.Date > DateTime.Today) throw new MemberValidationException("Date of birth cannot be in the future.");
        if (exitDate.HasValue && exitDate.Value.Date < joinDate.Date) throw new MemberValidationException("Exit date cannot be earlier than joining date.");
        if (contributionOverride.HasValue && contributionOverride.Value < 0m) throw new MemberValidationException("Contribution amount override cannot be negative.");
        ValidateEmail(email);
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var value = email.Trim();
        if (value.Length > 254) throw new MemberValidationException("Email cannot exceed 254 characters.");
        try { _ = new MailAddress(value); }
        catch (FormatException) { throw new MemberValidationException("Email address is invalid."); }
    }

    private static void ValidateNominees(IEnumerable<CreateMemberNomineeRequest>? nominees)
    {
        if (nominees is null) return;
        foreach (var nominee in nominees)
            ValidateNominee(nominee.FullName, nominee.SharePercentage);
    }

    private static void ValidateNominee(string fullName, decimal? sharePercentage)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new MemberValidationException("Nominee full name is required.");
        if (fullName.Trim().Length > 200) throw new MemberValidationException("Nominee full name cannot exceed 200 characters.");
        if (sharePercentage is < 0m or > 100m) throw new MemberValidationException("Nominee share percentage must be between 0 and 100.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MemberResponse Map(Member member) => new(
        member.Id, member.GroupId, member.Group.Code, member.Group.Name, member.MemberNumber, member.FullName,
        member.Address, member.MobileNumber, member.Email, member.DateOfBirth, member.JoinDate, member.ExitDate,
        member.IsActive, member.CitizenshipNumber, member.ContributionAmountOverride, member.Notes,
        member.Nominees.OrderBy(x => x.Id).Select(MapNominee).ToArray(), member.CreatedAtUtc, member.CreatedByUserId,
        member.ModifiedAtUtc, member.ModifiedByUserId);

    private static MemberNomineeResponse MapNominee(MemberNominee nominee) => new(
        nominee.Id, nominee.MemberId, nominee.FullName, nominee.Relationship, nominee.MobileNumber, nominee.Address,
        nominee.SharePercentage, nominee.IsActive, nominee.CreatedAtUtc, nominee.CreatedByUserId,
        nominee.ModifiedAtUtc, nominee.ModifiedByUserId);
}
