namespace GroupFinance.Core.Security;

public static class RoleNames
{
    public const string SuperAdministrator = "SuperAdministrator";
    public const string GroupAdministrator = "GroupAdministrator";
    public const string Accountant = "Accountant";
    public const string GroupViewer = "GroupViewer";

    public static readonly string[] All =
    [SuperAdministrator, GroupAdministrator, Accountant, GroupViewer];
}
