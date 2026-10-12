using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Hosting;

public static class EndpointDiscovery
{
    public static IServiceCollection AddFeatureEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpointTypes = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && typeof(IEndpoint).IsAssignableFrom(type));

        foreach (var type in endpointTypes)
        {
            services.AddSingleton(typeof(IEndpoint), type);
        }

        return services;
    }

    public static WebApplication MapFeatures(this WebApplication app)
    {
        foreach (var endpoint in app.Services.GetServices<IEndpoint>())
        {
            endpoint.Map(app);
        }

        return app;
    }
}
