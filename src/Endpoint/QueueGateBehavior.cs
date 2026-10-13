using Enrollment;
using Microsoft.Extensions.Configuration;
using NServiceBus;
using NServiceBus.Pipeline;

namespace EnrollmentEndpoint;

public sealed class QueueGateBehavior(string endpointName, IConfiguration configuration) : Behavior<IIncomingLogicalMessageContext>
{
    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        var messageName = context.Message.MessageType.Name;
        var mode = QueueGate.Effective(configuration, endpointName, messageName);
        if (mode == QueueGate.Error)
        {
            throw new RoutedToErrorException(
                $"Queue gate routed {endpointName}/{messageName} to the error queue.");
        }

        if (QueueGate.Pauses(mode, messageName))
        {
            var options = new SendOptions();
            options.RouteToThisEndpoint();
            options.DelayDeliveryWith(TimeSpan.FromSeconds(30));
            await context.Send(context.Message.Instance, options);
            return;
        }

        await next();
    }
}

public sealed class RoutedToErrorException(string message) : Exception(message);
