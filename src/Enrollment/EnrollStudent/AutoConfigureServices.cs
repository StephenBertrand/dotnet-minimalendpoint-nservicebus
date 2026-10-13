using Endpoint.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Enrollment.EnrollStudent;

public sealed class AutoConfigureServices : IFeatureHandlers
{
    public void Configure(IServiceCollection services) =>
        services.AddScoped<EnrollStudent>();
}
