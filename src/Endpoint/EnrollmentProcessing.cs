using Enrollment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;

namespace EnrollmentEndpoint;

public static class EnrollmentProcessing
{
    public static TBuilder AddEnrollmentProcessingEndpoints<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<EnrollmentProcessor>();
        builder.Services.AddSingleton<OrderProcessor>();

        var connectionString = builder.Configuration.GetConnectionString(MessagingTransport.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder;
        }

        var enrollment = CreateEndpoint(QueueGate.EnrollmentEndpoint, connectionString, builder.Configuration);
        enrollment.AddHandler<EnrollStudentHandler>();

        var orders = CreateEndpoint(QueueGate.OrdersEndpoint, connectionString, builder.Configuration);
        orders.AddHandler<WelcomeOrderHandler>();
        orders.AddHandler<CreateWelcomeOrderHandler>();

        builder.Services.AddNServiceBusEndpoint(enrollment, QueueGate.EnrollmentEndpoint);
        builder.Services.AddNServiceBusEndpoint(orders, QueueGate.OrdersEndpoint);

        return builder;
    }

    static EndpointConfiguration CreateEndpoint(string endpointName, string connectionString, IConfiguration configuration)
    {
        var endpoint = new EndpointConfiguration(endpointName);
        endpoint.AssemblyScanner().Disable = true;
        endpoint.EnableInstallers();
        endpoint.UseSerialization<SystemJsonSerializer>();
        var conventions = endpoint.Conventions();
        conventions.DefiningCommandsAs(MessageConventions.IsCommand);
        conventions.DefiningEventsAs(MessageConventions.IsEvent);

        var transport = MessagingTransport.Create(connectionString);
        // ReceiveOnly keeps the SQL write out of the broker transaction. A crash between send and
        // complete can duplicate or lose a message. That matches the sample's no-outbox choice.
        transport.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        endpoint.UseTransport(transport);
        endpoint.SendFailedMessagesTo(ServicePlatformQueues.Error);
        endpoint.AuditProcessedMessagesTo(ServicePlatformQueues.Audit);
        endpoint.SendHeartbeatTo(ServicePlatformQueues.Heartbeats);
        endpoint.EnableMetrics().SendMetricDataToServiceControl(
            ServicePlatformQueues.Monitoring,
            TimeSpan.FromSeconds(10));
        endpoint.Recoverability().AddUnrecoverableException<RoutedToErrorException>();
        endpoint.Pipeline.Register(
            _ => new QueueGateBehavior(endpointName, configuration),
            $"Apply the queue gate for {endpointName}");

        return endpoint;
    }
}
