using Endpoint.Extensions;
using Messages;
using Routing;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace Orders.CreateWelcomeOrder;

[Handler]
[HandleOn("Orders")]
public sealed class EnrollmentCompletedEventHandler(ILogger<EnrollmentCompletedEventHandler> logger)
{
    public async Task Handle(EnrollmentCompletedEvent message, IMessageHandlerContext context)
    {
        using (logger.BeginHandlerScope(context.MessageId, message.TrackingId))
        {
            logger.LogMessageReceived(message);
            var command = Map(message);
            await context.Send(command);
            logger.LogMessageSent(command);
        }
    }

    static CreateWelcomeOrderCommand Map(EnrollmentCompletedEvent message) => new()
    {
        TrackingId = message.TrackingId,
        Name = message.Name,
        Email = message.Email,
        Program = message.Program
    };
}

[Handler]
public sealed class CreateWelcomeOrderCommandHandler(
    CreateWelcomeOrder createWelcomeOrder,
    ILogger<CreateWelcomeOrderCommandHandler> logger)
{
    public async Task Handle(CreateWelcomeOrderCommand message, IMessageHandlerContext context)
    {
        using (logger.BeginHandlerScope(context.MessageId, message.TrackingId))
        {
            logger.LogMessageReceived(message);
            await createWelcomeOrder.ExecuteAsync(message, context.CancellationToken);
            logger.LogInformation("Completed {TrackingId}", message.TrackingId);
        }
    }
}
