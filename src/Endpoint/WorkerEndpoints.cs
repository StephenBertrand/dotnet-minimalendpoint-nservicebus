using Endpoint.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using Transport;

namespace EnrollmentEndpoint;

public static class WorkerEndpoints
{
    public static TBuilder AddWorkerEndpoints<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var features = FeatureModules.Discover(typeof(WorkerEndpoints).Assembly);
        foreach (var feature in features)
        {
            feature.AddStorage(builder);
            feature.Configure(builder.Services);
        }

        var connectionString = builder.Configuration.GetConnectionString(MessagingTransport.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder;
        }

        foreach (var endpointName in Destinations(features))
        {
            var endpoint = CreateEndpoint(endpointName, connectionString, builder.Configuration);
            foreach (var feature in features)
            {
                feature.Register(endpointName, endpoint);
            }

            builder.Services.AddNServiceBusEndpoint(endpoint, endpointName);
        }

        return builder;
    }

    static IReadOnlyList<string> Destinations(IReadOnlyList<IFeatureHandlers> features) =>
        features
            .Select(feature => feature.GetType().Assembly)
            .Distinct()
            .SelectMany(FeatureHandlers.Scan)
            .Select(binding => binding.Destination)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    static EndpointConfiguration CreateEndpoint(string endpointName, string connectionString, IConfiguration configuration)
    {
        var endpoint = new EndpointConfiguration(endpointName);
        endpoint.ConfigureReceiveOnlyEndpoint(connectionString);
        endpoint.SendHeartbeatTo(ServicePlatformQueues.Heartbeats);
        endpoint.AuditProcessedMessagesTo(ServicePlatformQueues.Audit);
        endpoint.EnableMetrics().SendMetricDataToServiceControl(
            ServicePlatformQueues.Monitoring,
            ServicePlatformQueues.MetricsInterval);
        endpoint.Recoverability().AddUnrecoverableException<RoutedToErrorException>();
        endpoint.Pipeline.Register(
            _ => new QueueGateBehavior(endpointName, configuration),
            $"Apply the queue gate for {endpointName}");
        return endpoint;
    }
}
