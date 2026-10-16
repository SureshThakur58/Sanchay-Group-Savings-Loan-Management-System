using System.Net;

namespace GroupFinance.IntegrationTests;

/// <summary>
/// Phase 27 API smoke/security tests. These tests use the dedicated test host
/// so database and JWT configuration are applied before application startup.
/// </summary>
public sealed class Phase27ApiSecurityIntegrationTests : IClassFixture<Phase27WebApplicationFactory>
{
    private readonly Phase27WebApplicationFactory _factory;

    public Phase27ApiSecurityIntegrationTests(Phase27WebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Public_Health_Endpoint_Is_Reachable()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_Me_Endpoint_Rejects_Anonymous_Request()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_Members_Endpoint_Rejects_Anonymous_Request()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/members?groupId=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
