using Endpoint.Extensions;
using Orders;
using Microsoft.Extensions.Hosting;

namespace Orders.Shared;

public sealed class AutoConfigureServices : IFeatureHandlers
{
    public void AddStorage(IHostApplicationBuilder builder) => builder.AddOrderStore();

    public Task PrepareStorageAsync(IHost host, CancellationToken cancellationToken) =>
        host.ApplyOrderSchemaAsync(cancellationToken);
}
