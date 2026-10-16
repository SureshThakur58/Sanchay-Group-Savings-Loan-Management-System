namespace GroupFinance.Core.Security;

public static class PermissionCodes
{
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";
    public const string RolesView = "Roles.View";
    public const string RolesManage = "Roles.Manage";
    public const string SystemSettingsManage = "SystemSettings.Manage";
    public const string GroupsView = "Groups.View";
    public const string GroupsManage = "Groups.Manage";
    public const string MembersView = "Members.View";
    public const string MembersManage = "Members.Manage";
    public const string ContributionsView = "Contributions.View";
    public const string ContributionsManage = "Contributions.Manage";
    public const string LoansView = "Loans.View";
    public const string LoansManage = "Loans.Manage";
    public const string FinancialTransactionsView = "FinancialTransactions.View";
    public const string FinancialTransactionsManage = "FinancialTransactions.Manage";
    public const string ExpensesView = "Expenses.View";
    public const string ExpensesManage = "Expenses.Manage";
    public const string AssetsView = "Assets.View";
    public const string AssetsManage = "Assets.Manage";
    public const string ReportsView = "Reports.View";
    public const string InterestProfitView = "InterestProfit.View";
    public const string InterestProfitManage = "InterestProfit.Manage";
    public const string AuditLogsView = "AuditLogs.View";
    public const string AuditLogsManage = "AuditLogs.Manage";
    public const string NotificationsView = "Notifications.View";
    public const string NotificationsManage = "Notifications.Manage";
    public const string AttachmentsView = "Attachments.View";
    public const string AttachmentsManage = "Attachments.Manage";
    public const string BackupsView = "Backups.View";
    public const string BackupsManage = "Backups.Manage";

    public static readonly string[] All =
    [
        UsersView, UsersManage, RolesView, RolesManage, SystemSettingsManage,
        GroupsView, GroupsManage, MembersView, MembersManage,
        ContributionsView, ContributionsManage, LoansView, LoansManage,
        FinancialTransactionsView, FinancialTransactionsManage,
        ExpensesView, ExpensesManage, AssetsView, AssetsManage, ReportsView, InterestProfitView, InterestProfitManage,
        AuditLogsView, AuditLogsManage, NotificationsView, NotificationsManage,
        AttachmentsView, AttachmentsManage, BackupsView, BackupsManage
    ];
}
