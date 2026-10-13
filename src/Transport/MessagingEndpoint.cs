using NServiceBus;
using Routing;

namespace Transport;

public static class MessagingEndpoint
{
    public static void ConfigureReceiveOnlyEndpoint(this EndpointConfiguration endpoint, string connectionString)
    {
        endpoint.AssemblyScanner().Disable = true;
        endpoint.EnableInstallers();
        endpoint.UseSerialization<SystemJsonSerializer>();

        var conventions = endpoint.Conventions();
        conventions.DefiningCommandsAs(MessageConventions.IsCommand);
        conventions.DefiningEventsAs(MessageConventions.IsEvent);

        var transport = MessagingTransport.Create(connectionString);
        // ReceiveOnly keeps the SQL write out of the broker transaction. This sample has no outbox.
        transport.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        endpoint.UseTransport(transport).ApplyAttributeRoutes();

        endpoint.SendFailedMessagesTo(ServicePlatformQueues.Error);
    }
}
