using System.Net;

namespace GroupFinance.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<Phase27WebApplicationFactory>
{
    private readonly Phase27WebApplicationFactory _factory;

    public HealthEndpointTests(Phase27WebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Ok()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
