using Api.Features.Enrollments;
using Azure.Identity;
using Enrollment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;

namespace Api.Hosting;

public static class EnrollmentMessaging
{
    public static TBuilder AddEnrollmentMessaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var connectionString = builder.Configuration.GetConnectionString(MessagingTransport.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddSingleton<IEnrollmentSender, UnavailableEnrollmentSender>();
            return builder;
        }

        var endpoint = new EndpointConfiguration("Api");
        endpoint.SendOnly();
        endpoint.EnableInstallers();
        endpoint.UseSerialization<SystemJsonSerializer>();
        endpoint.Conventions().DefiningCommandsAs(type => type == typeof(EnrollStudent));

        var transport = ServiceBusTransport.Create(connectionString);
        transport.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        var routing = endpoint.UseTransport(transport);
        routing.RouteToEndpoint(typeof(EnrollStudent), EnrollmentEndpointName.Value);
        endpoint.SendFailedMessagesTo(ServicePlatformQueues.Error);
        endpoint.SendHeartbeatTo(ServicePlatformQueues.Heartbeats);

        builder.Services.AddNServiceBusEndpoint(endpoint);

        builder.Services.AddSingleton<IEnrollmentSender, NServiceBusEnrollmentSender>();
        return builder;
    }
}

static class EnrollmentEndpointName
{
    public const string Value = "Enrollment";
}

sealed class UnavailableEnrollmentSender : IEnrollmentSender
{
    public Task SendAsync(EnrollStudent message, bool delay, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Enrollment messaging needs the messaging connection string before it can send.");
}

static class ServiceBusTransport
{
    internal static AzureServiceBusTransport Create(string connectionString)
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

    static string NormalizeEmulatorConnectionString(string connectionString)
    {
        if (!connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return connectionString.Replace(":5672;", ";", StringComparison.Ordinal);
    }
}

sealed class NServiceBusEnrollmentSender(IMessageSession session) : IEnrollmentSender
{
    public Task SendAsync(EnrollStudent message, bool delay, CancellationToken cancellationToken)
    {
        var options = new SendOptions();
        if (delay)
        {
            options.DelayDeliveryWith(ProcessingModes.Delay);
        }

        return session.Send(message, options, cancellationToken);
    }
}
