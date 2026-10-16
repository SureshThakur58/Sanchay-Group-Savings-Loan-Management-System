using System.Reflection;
using GroupFinance.Api.Controllers;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GroupFinance.UnitTests;

public sealed class ApiSecurityContractTests
{
    private static readonly Assembly ApiAssembly = typeof(HealthController).Assembly;

    [Fact]
    public void Every_Api_Controller_Requires_Authentication()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(t => t != typeof(HealthController) && t != typeof(DatabaseHealthController))
            .ToArray();

        Assert.NotEmpty(controllers);
        Assert.All(controllers, controller =>
        {
            var controllerAllowsAnonymous = controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
            var controllerRequiresAuth = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
            var hasAuthorizedAction = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Any(method => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any());

            Assert.True(
                controllerAllowsAnonymous || controllerRequiresAuth || hasAuthorizedAction,
                $"{controller.Name} has no authentication requirement.");
        });
    }

    [Fact]
    public void Every_Http_Action_Has_Explicit_Authorization_Metadata()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller", StringComparison.Ordinal));

        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var isHttpAction = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any();
                if (!isHttpAction) continue;

                var hasAuthorization = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
                    || controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
                var allowsAnonymous = method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

                Assert.True(
                    hasAuthorization || allowsAnonymous || controller == typeof(HealthController) || controller == typeof(DatabaseHealthController),
                    $"{controller.Name}.{method.Name} has no authorization metadata.");
            }
        }
    }

    [Theory]
    [InlineData(nameof(LoanApplicationsController.Approve))]
    [InlineData(nameof(LoanApplicationsController.Reject))]
    public void Loan_Decision_Actions_Require_Administrator_Role(string methodName)
    {
        var method = typeof(LoanApplicationsController).GetMethod(methodName)!;
        var roles = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(x => x.Roles)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        Assert.Contains(
            roles,
            value => value!.Contains(RoleNames.SuperAdministrator, StringComparison.OrdinalIgnoreCase)
                  && value.Contains(RoleNames.GroupAdministrator, StringComparison.OrdinalIgnoreCase));
    }
}
