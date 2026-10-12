using System.Net.Http.Json;
using Api.Features.Hello;

namespace Api.Tests;

public class HelloEndpointsTests
{
    [Fact]
    public async Task Hello_WhenFlagOff_ReturnsHelloWorld()
    {
        await using var factory = new HelloApiFactory(helloNewWorld: false);
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<HelloResponse>("/hello");

        Assert.NotNull(response);
        Assert.Equal("Hello World", response.Message);
        Assert.Equal(HelloFeatures.NewWorld, response.FeatureFlag);
        Assert.False(response.FeatureEnabled);
    }

    [Fact]
    public async Task Hello_WhenFlagOn_ReturnsHelloNewWorld()
    {
        await using var factory = new HelloApiFactory(helloNewWorld: true);
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<HelloResponse>("/hello");

        Assert.NotNull(response);
        Assert.Equal("Hello New World", response.Message);
        Assert.True(response.FeatureEnabled);
    }
}
