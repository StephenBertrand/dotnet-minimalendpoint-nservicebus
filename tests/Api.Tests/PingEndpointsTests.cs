using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

public class PingEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PingEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pong", body, StringComparison.OrdinalIgnoreCase);
    }
}
