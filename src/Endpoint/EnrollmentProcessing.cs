using Azure.Identity;
using Enrollment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using NServiceBus;

namespace EnrollmentEndpoint;

public static class EnrollmentProcessing
{
    public const string EndpointName = "Enrollment";

    public static TBuilder AddEnrollmentProcessingEndpoint<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<EnrollmentProcessor>();

        var connectionString = builder.Configuration.GetConnectionString(MessagingTransport.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder;
        }

        var endpoint = new EndpointConfiguration(EndpointName);
        endpoint.EnableInstallers();
        endpoint.UseSerialization<SystemJsonSerializer>();
        endpoint.Conventions().DefiningCommandsAs(type => type == typeof(EnrollStudent));
        var transport = CreateServiceBusTransport(connectionString);
        // The emulator cannot enlist the audit send in the receive. A crash between send and
        // complete can duplicate or lose a message. That matches the sample's no-outbox choice.
        transport.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        endpoint.UseTransport(transport);
        endpoint.SendFailedMessagesTo(ServicePlatformQueues.Error);
        endpoint.AuditProcessedMessagesTo(ServicePlatformQueues.Audit);
        endpoint.SendHeartbeatTo(ServicePlatformQueues.Heartbeats);
        endpoint.EnableMetrics().SendMetricDataToServiceControl(
            ServicePlatformQueues.Monitoring,
            TimeSpan.FromSeconds(10));

        endpoint.Pipeline.Register(
            services => new StopProcessingBehavior(services.GetRequiredService<IFeatureManager>()),
            "Hold messages while the StopProcessing flag is on");

        builder.Services.AddNServiceBusEndpoint(endpoint);

        return builder;
    }

    static AzureServiceBusTransport CreateServiceBusTransport(string connectionString)
    {
        connectionString = NormalizeEmulatorConnectionString(connectionString);
        if (connectionString.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase))
        {
            return new AzureServiceBusTransport(connectionString, TopicTopology.Default);
        }

        var fullyQualifiedNamespace = connectionString;
        const string scheme = "sb://";
        if (fullyQualifiedNamespace.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            fullyQualifiedNamespace = fullyQualifiedNamespace[scheme.Length..];
        }

        var slash = fullyQualifiedNamespace.IndexOf('/');
        if (slash >= 0)
        {
            fullyQualifiedNamespace = fullyQualifiedNamespace[..slash];
        }

        return new AzureServiceBusTransport(fullyQualifiedNamespace, new DefaultAzureCredential(), TopicTopology.Default);
    }

    // NServiceBus only moves emulator administration to port 5300 when the endpoint has no port.
    // Aspire publishes AMQP on 5672, which would otherwise send administration calls to the broker port.
    static string NormalizeEmulatorConnectionString(string connectionString)
    {
        if (!connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return connectionString.Replace(":5672;", ";", StringComparison.Ordinal);
    }
}
