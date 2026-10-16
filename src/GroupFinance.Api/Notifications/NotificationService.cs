using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Notifications;

public sealed class NotificationValidationException(string message) : Exception(message);

public sealed class NotificationService(GroupFinanceDbContext db, ILogger<NotificationService> logger)
{
    public async Task<NotificationListResponse> ListAsync(long userId, bool unreadOnly, string? type, CancellationToken ct)
    {
        if (userId <= 0) throw new NotificationValidationException("Authenticated user is required.");
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        var normalizedType = type?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedType)) query = query.Where(x => x.NotificationType == normalizedType);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Take(200).ToListAsync(ct);
        var unread = await db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, ct);
        return new NotificationListResponse(rows.Select(Map).ToArray(), unread);
    }

    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken ct)
    {
        if (userId <= 0) throw new NotificationValidationException("Authenticated user is required.");
        return await db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, ct);
    }

    public async Task MarkReadAsync(long id, long userId, CancellationToken ct)
    {
        var n = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) throw new NotificationValidationException("Notification not found.");
        if (!n.IsRead) { var now=DateTime.UtcNow; n.IsRead=true; n.ReadAtUtc=now; n.ModifiedAtUtc=now; n.ModifiedByUserId=userId; await db.SaveChangesAsync(ct); }
    }

    public async Task MarkAllReadAsync(long userId, CancellationToken ct)
    {
        var rows = await db.Notifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync(ct);
        if (rows.Count == 0) return;
        var now=DateTime.UtcNow; foreach(var n in rows){n.IsRead=true;n.ReadAtUtc=now;n.ModifiedAtUtc=now;n.ModifiedByUserId=userId;}
        await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationEvaluationResponse> EvaluateAsync(long groupId, DateTime? asOfDate, int daysAhead, CancellationToken ct)
    {
        if (groupId <= 0) throw new NotificationValidationException("groupId is required.");
        if (daysAhead < 0 || daysAhead > 31) throw new NotificationValidationException("daysAhead must be between 0 and 31.");
        if (!await db.Groups.AsNoTracking().AnyAsync(x => x.Id == groupId && x.IsActive, ct)) throw new NotificationValidationException("The selected group does not exist or is inactive.");
        var date=(asOfDate ?? DateTime.UtcNow).Date; var horizon=date.AddDays(daysAhead);
        var recipients=await db.Users.AsNoTracking().Where(x=>x.IsActive).Select(x=>x.Id).ToArrayAsync(ct);
        if(recipients.Length==0) return new(0,0,0,0,0,0,0,0,0);
        int created=0, upcomingContributions=0, overdueContributions=0, upcomingLoanInstallments=0, overdueLoans=0, loanApprovals=0, loanDisbursements=0, paymentReceipts=0, profitDistributions=0;

        var cs=await db.ContributionSchedules.AsNoTracking().Include(x=>x.Member).Where(x=>x.Member.GroupId==groupId && x.DueDate<=horizon).OrderBy(x=>x.DueDate).Take(500).ToListAsync(ct);
        foreach(var x in cs){ var outstanding=Math.Max(0m,x.DueAmount+x.LateFeeAmount-x.WaivedLateFeeAmount-x.PaidAmount); if(outstanding<=0) continue; if(x.DueDate.Date<date){ created+=await CreateForRecipientsAsync(recipients,NotificationTypes.OverdueContribution,"Overdue contribution",$"{x.Member.MemberNumber} - {x.Member.FullName}: contribution due {x.DueDate:dd/MM/yyyy} has NPR {outstanding:N2} outstanding.",ct); overdueContributions++; } else { created+=await CreateForRecipientsAsync(recipients,NotificationTypes.UpcomingContribution,"Upcoming contribution",$"{x.Member.MemberNumber} - {x.Member.FullName}: contribution of NPR {outstanding:N2} is due on {x.DueDate:dd/MM/yyyy}.",ct); upcomingContributions++; }}

        var ls=await db.LoanSchedules.AsNoTracking().Include(x=>x.Loan).ThenInclude(x=>x.Member).Where(x=>x.Loan.GroupId==groupId && x.Loan.Status=="Active" && x.DueDate<=horizon).OrderBy(x=>x.DueDate).Take(1000).ToListAsync(ct);
        var overdueIds=new HashSet<long>();
        foreach(var x in ls){ var outstanding=Math.Max(0m,x.PrincipalDue-x.PrincipalPaid)+Math.Max(0m,x.InterestDue-x.InterestPaid)+Math.Max(0m,x.PenaltyDue-x.PenaltyPaid); if(outstanding<=0) continue; if(x.DueDate.Date<date){overdueIds.Add(x.LoanId); created+=await CreateForRecipientsAsync(recipients,NotificationTypes.OverdueLoan,"Overdue loan",$"{x.Loan.Member.MemberNumber} - {x.Loan.Member.FullName}: loan {x.Loan.LoanNumber} installment #{x.InstallmentNumber} is overdue by {(date-x.DueDate.Date).Days} day(s), outstanding NPR {outstanding:N2}.",ct);} else {created+=await CreateForRecipientsAsync(recipients,NotificationTypes.LoanInstallmentReminder,"Upcoming loan installment",$"{x.Loan.Member.MemberNumber} - {x.Loan.Member.FullName}: loan {x.Loan.LoanNumber} installment #{x.InstallmentNumber} of NPR {outstanding:N2} is due on {x.DueDate:dd/MM/yyyy}.",ct); upcomingLoanInstallments++;}}
        overdueLoans=overdueIds.Count;

        var recentStart=date.AddDays(-1);
        var apps=await db.LoanApplications.AsNoTracking().Include(x=>x.Member).Where(x=>x.Member.GroupId==groupId && x.Status=="Approved" && x.ModifiedAtUtc.HasValue && x.ModifiedAtUtc.Value.Date>=recentStart && x.ModifiedAtUtc.Value.Date<=date).Take(200).ToListAsync(ct);
        foreach(var x in apps){created+=await CreateForRecipientsAsync(recipients,NotificationTypes.LoanApproval,"Loan approval",$"Loan application {x.LoanApplicationNumber} for {x.Member.MemberNumber} - {x.Member.FullName} has been approved for NPR {x.RequestedAmount:N2}.",ct); loanApprovals++;}

        var loans=await db.Loans.AsNoTracking().Include(x=>x.Member).Where(x=>x.GroupId==groupId && x.DisbursementDate.Date>=recentStart && x.DisbursementDate.Date<=date).Take(200).ToListAsync(ct);
        foreach(var x in loans){created+=await CreateForRecipientsAsync(recipients,NotificationTypes.LoanDisbursement,"Loan disbursement",$"Loan {x.LoanNumber} was disbursed to {x.Member.MemberNumber} - {x.Member.FullName} for NPR {x.PrincipalAmount:N2}.",ct); loanDisbursements++;}

        var lp=await db.LoanPayments.AsNoTracking().Include(x=>x.Loan).ThenInclude(x=>x.Member).Where(x=>x.Loan.GroupId==groupId && x.TransactionDate.Date>=recentStart && x.TransactionDate.Date<=date && x.Status=="Posted").Take(500).ToListAsync(ct);
        foreach(var x in lp){created+=await CreateForRecipientsAsync(recipients,NotificationTypes.PaymentReceipt,"Payment receipt",$"Loan repayment of NPR {x.Amount:N2} received from {x.Loan.Member.MemberNumber} - {x.Loan.Member.FullName} on {x.TransactionDate:dd/MM/yyyy}.",ct); paymentReceipts++;}
        var cp=await db.ContributionPayments.AsNoTracking().Include(x=>x.Member).Where(x=>x.Member.GroupId==groupId && x.TransactionDate.Date>=recentStart && x.TransactionDate.Date<=date && x.Status=="Posted").Take(500).ToListAsync(ct);
        foreach(var x in cp){created+=await CreateForRecipientsAsync(recipients,NotificationTypes.PaymentReceipt,"Payment receipt",$"Contribution payment of NPR {x.Amount:N2} received from {x.Member.MemberNumber} - {x.Member.FullName} on {x.TransactionDate:dd/MM/yyyy}.",ct); paymentReceipts++;}
        var pd=await db.ProfitDistributions.AsNoTracking().Where(x=>x.GroupId==groupId && x.Status=="Distributed" && x.DistributedDate.HasValue && x.DistributedDate.Value.Date>=recentStart && x.DistributedDate.Value.Date<=date).Take(100).ToListAsync(ct);
        foreach(var x in pd){created+=await CreateForRecipientsAsync(recipients,NotificationTypes.ProfitDistribution,"Profit distribution",$"Profit distribution for {x.PeriodStart:dd/MM/yyyy} to {x.PeriodEnd:dd/MM/yyyy} was completed for NPR {x.DistributableAmount:N2}.",ct); profitDistributions++;}
        return new(created,upcomingContributions,overdueContributions,upcomingLoanInstallments,overdueLoans,loanApprovals,loanDisbursements,paymentReceipts,profitDistributions);
    }

    private async Task<int> CreateForRecipientsAsync(IReadOnlyCollection<long> recipients,string type,string title,string message,CancellationToken ct)
    {
        var existing=await db.Notifications.AsNoTracking().Where(x=>recipients.Contains(x.UserId??0)&&x.NotificationType==type&&x.Title==title&&x.Message==message).Select(x=>x.UserId!.Value).ToListAsync(ct);
        var set=existing.ToHashSet(); int added=0; var now=DateTime.UtcNow; foreach(var userId in recipients) if(!set.Contains(userId)){db.Notifications.Add(new Notification{UserId=userId,NotificationType=type,Title=title,Message=message,IsRead=false,CreatedAtUtc=now});added++;} if(added>0) await db.SaveChangesAsync(ct); return added;
    }

    public async Task TryNotifyGroupAsync(long groupId,string type,string title,string message,CancellationToken ct)
    { try { var ids=await db.Users.AsNoTracking().Where(x=>x.IsActive).Select(x=>x.Id).ToArrayAsync(ct); if(ids.Length>0) await CreateForRecipientsAsync(ids,type,title,message,ct); } catch(Exception ex){logger.LogWarning(ex,"Notification creation failed for group {GroupId}.",groupId);} }
    private static NotificationResponse Map(Notification x)=>new(x.Id,x.UserId,x.NotificationType,x.Title,x.Message,x.IsRead,x.ReadAtUtc,x.CreatedAtUtc);
}
