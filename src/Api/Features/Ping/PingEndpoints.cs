using Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.Features.Ping;

public sealed class PingEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/ping", () => Results.Ok(new { status = "pong" }))
            .WithName("Ping")
            .WithTags("Ping");
    }
}
