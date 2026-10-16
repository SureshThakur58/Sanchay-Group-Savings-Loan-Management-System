namespace GroupFinance.Api.Notifications;

public static class NotificationTypes
{
    public const string UpcomingContribution = "UpcomingContribution";
    public const string OverdueContribution = "OverdueContribution";
    public const string LoanInstallmentReminder = "LoanInstallmentReminder";
    public const string OverdueLoan = "OverdueLoan";
    public const string LoanApproval = "LoanApproval";
    public const string LoanDisbursement = "LoanDisbursement";
    public const string PaymentReceipt = "PaymentReceipt";
    public const string ProfitDistribution = "ProfitDistribution";

    public static readonly string[] All = [UpcomingContribution, OverdueContribution, LoanInstallmentReminder, OverdueLoan, LoanApproval, LoanDisbursement, PaymentReceipt, ProfitDistribution];
}

public sealed record NotificationResponse(long Id, long? UserId, string NotificationType, string Title, string Message, bool IsRead, DateTime? ReadAtUtc, DateTime CreatedAtUtc);
public sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Notifications, int UnreadCount);
public sealed record NotificationEvaluationResponse(int CreatedCount, int UpcomingContributions, int OverdueContributions, int UpcomingLoanInstallments, int OverdueLoans, int LoanApprovals, int LoanDisbursements, int PaymentReceipts, int ProfitDistributions);
