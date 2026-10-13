using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endpoint.Extensions;

public interface IFeatureHandlers
{
    void AddStorage(IHostApplicationBuilder builder)
    {
    }

    Task PrepareStorageAsync(IHost host, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    void Configure(IServiceCollection services)
    {
    }

    void Register(string endpointName, object endpointConfiguration) =>
        FeatureHandlers.AddFrom(GetType(), endpointConfiguration, endpointName);
}
