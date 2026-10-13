using Endpoint.Extensions;
using Enrollment;
using Microsoft.Extensions.Hosting;

namespace Enrollment.Shared;

public sealed class AutoConfigureServices : IFeatureHandlers
{
    public void AddStorage(IHostApplicationBuilder builder) => builder.AddEnrollmentStore();

    public Task PrepareStorageAsync(IHost host, CancellationToken cancellationToken) =>
        host.ApplyEnrollmentSchemaAsync(cancellationToken);
}
