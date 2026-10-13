using System.Net;
using System.Net.Http.Json;

namespace Api.Tests;

public class ProcessingEndpointsTests
{
    [Fact]
    public async Task Get_WhenStoreMissing_ReportsEveryGateRunningAndNotUpdatable()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<ProcessingCatalog>("/processing");

        Assert.NotNull(response);
        Assert.False(response.CanUpdate);
        Assert.Equal(2, response.Endpoints.Length);
        Assert.All(response.Endpoints, endpoint => Assert.Equal("running", endpoint.Mode));
        Assert.Contains(response.Endpoints, endpoint => endpoint.Name == "Enrollment");
        Assert.Contains(response.Endpoints, endpoint => endpoint.Name == "Orders");
    }

    [Fact]
    public async Task Put_WhenStoreMissing_Returns503()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/processing", new { endpoint = "Enrollment", mode = "pause" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed record ProcessingCatalog(bool CanUpdate, ProcessingEndpoint[] Endpoints);

    private sealed record ProcessingEndpoint(string Name, string Label, string Mode, ProcessingHandler[] Handlers);

    private sealed record ProcessingHandler(string Message, string Label, string Mode);
}
