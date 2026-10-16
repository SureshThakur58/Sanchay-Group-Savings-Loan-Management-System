using GroupFinance.Core.Security;

namespace GroupFinance.UnitTests;

public sealed class AuthenticationTests
{
    [Fact]
    public void RequiredRolesAreDefined()
    {
        Assert.Equal(4, RoleNames.All.Length);
        Assert.Contains(RoleNames.SuperAdministrator, RoleNames.All);
        Assert.Contains(RoleNames.GroupAdministrator, RoleNames.All);
        Assert.Contains(RoleNames.Accountant, RoleNames.All);
        Assert.Contains(RoleNames.GroupViewer, RoleNames.All);
    }

    [Fact]
    public void PermissionCatalogContainsCoreAdministrationAndFinancialPermissions()
    {
        Assert.Contains(PermissionCodes.UsersManage, PermissionCodes.All);
        Assert.Contains(PermissionCodes.FinancialTransactionsManage, PermissionCodes.All);
        Assert.Contains(PermissionCodes.ReportsView, PermissionCodes.All);
    }
}
