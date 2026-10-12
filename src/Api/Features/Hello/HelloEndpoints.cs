using Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.FeatureManagement;

namespace Api.Features.Hello;

public sealed class HelloEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/hello")
            .WithTags("Hello");

        group.MapGet("/", GetHelloAsync)
            .WithName("GetHello")
            .WithSummary("Returns Hello World, or Hello New World when the HelloNewWorld feature flag is on.");
    }

    private static async Task<IResult> GetHelloAsync(IFeatureManager features)
    {
        var enabled = await features.IsEnabledAsync(HelloFeatures.NewWorld);
        var message = enabled ? "Hello New World" : "Hello World";

        return Results.Ok(new HelloResponse(message, HelloFeatures.NewWorld, enabled));
    }
}
