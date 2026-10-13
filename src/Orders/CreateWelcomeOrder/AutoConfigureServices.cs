using Endpoint.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.CreateWelcomeOrder;

public sealed class AutoConfigureServices : IFeatureHandlers
{
    public void Configure(IServiceCollection services) =>
        services.AddScoped<CreateWelcomeOrder>();
}
