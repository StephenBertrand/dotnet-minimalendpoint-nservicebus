using Azure.Identity;
using NServiceBus;
using NServiceBus.Transport;

namespace Transport;

public static class MessagingTransport
{
    public const string ConnectionName = "messaging";

    public static bool IsRabbitMq(string connectionString) =>
        connectionString.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase)
        || connectionString.StartsWith("amqps://", StringComparison.OrdinalIgnoreCase);

    public static TransportDefinition Create(string connectionString)
    {
        if (IsRabbitMq(connectionString))
        {
            return new RabbitMQTransport(
                RoutingTopology.Conventional(QueueType.Quorum),
                connectionString);
        }

        return CreateServiceBus(connectionString);
    }

    static AzureServiceBusTransport CreateServiceBus(string connectionString)
    {
        connectionString = NormalizeEmulatorConnectionString(connectionString);
        if (connectionString.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase))
        {
            return new AzureServiceBusTransport(connectionString, TopicTopology.Default);
        }

        return new AzureServiceBusTransport(NamespaceName(connectionString), new DefaultAzureCredential(), TopicTopology.Default);
    }

    static string NamespaceName(string connectionString)
    {
        const string scheme = "sb://";
        if (connectionString.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            connectionString = connectionString[scheme.Length..];
        }

        var slash = connectionString.IndexOf('/');
        return slash >= 0 ? connectionString[..slash] : connectionString;
    }

    // NServiceBus only moves emulator administration to port 5300 when the endpoint has no port.
    static string NormalizeEmulatorConnectionString(string connectionString)
    {
        if (!connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return connectionString.Replace(":5672;", ";", StringComparison.Ordinal);
    }
}
