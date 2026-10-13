using Api.Features.Enrollments;
using Enrollment;
using Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using Transport;

namespace Api.Hosting;

public static class ApiMessaging
{
    public const string EndpointName = "Api";

    public static TBuilder AddApiMessaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var connectionString = builder.Configuration.GetConnectionString(MessagingTransport.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddSingleton<IEnrollmentSender, UnavailableEnrollmentSender>();
            return builder;
        }

        var endpoint = new EndpointConfiguration(EndpointName);
        endpoint.SendOnly();
        endpoint.ConfigureReceiveOnlyEndpoint(connectionString);
        endpoint.SendHeartbeatTo(ServicePlatformQueues.Heartbeats);
        builder.Services.AddNServiceBusEndpoint(endpoint);
        builder.Services.AddSingleton<IEnrollmentSender, NServiceBusEnrollmentSender>();
        return builder;
    }
}

sealed class UnavailableEnrollmentSender : IEnrollmentSender
{
    public Task SendAsync(EnrollStudentCommand message, bool delay, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Enrollment messaging needs the messaging connection string before it can send.");
}

sealed class NServiceBusEnrollmentSender(IMessageSession session) : IEnrollmentSender
{
    public Task SendAsync(EnrollStudentCommand message, bool delay, CancellationToken cancellationToken)
    {
        var options = new SendOptions();
        if (delay)
        {
            options.DelayDeliveryWith(ProcessingModes.Delay);
        }

        return session.Send(message, options, cancellationToken);
    }
}
