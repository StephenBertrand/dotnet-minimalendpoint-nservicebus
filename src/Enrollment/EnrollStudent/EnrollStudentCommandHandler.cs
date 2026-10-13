using Enrollment;
using Endpoint.Extensions;
using Messages;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace Enrollment.EnrollStudent;

[Handler]
public sealed class EnrollStudentCommandHandler(
    EnrollStudent enroll,
    ILogger<EnrollStudentCommandHandler> logger)
{
    public async Task Handle(EnrollStudentCommand message, IMessageHandlerContext context)
    {
        using (logger.BeginHandlerScope(context.MessageId, message.TrackingId))
        {
            logger.LogMessageReceived(message);
            var outcome = await enroll.ProcessAsync(message, context.CancellationToken);
            if (outcome != EnrollmentOutcome.Completed)
            {
                return;
            }

            var completed = new EnrollmentCompletedEvent
            {
                TrackingId = message.TrackingId,
                Name = message.Name,
                Email = message.Email,
                Program = message.Program
            };
            await context.Publish(completed);
            logger.LogMessageSent(completed);
            logger.LogInformation("Completed {TrackingId}", message.TrackingId);
        }
    }
}
