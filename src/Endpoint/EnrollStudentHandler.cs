using Enrollment;
using NServiceBus;

namespace EnrollmentEndpoint;

[Handler]
public sealed class EnrollStudentHandler(EnrollmentProcessor processor) : IHandleMessages<EnrollStudentCommand>
{
    public async Task Handle(EnrollStudentCommand message, IMessageHandlerContext context)
    {
        var outcome = await processor.ProcessAsync(message, context.CancellationToken);
        if (outcome != EnrollmentOutcome.Completed)
        {
            return;
        }

        await context.Publish(new EnrollmentCompletedEvent
        {
            TrackingId = message.TrackingId,
            Name = message.Name,
            Email = message.Email,
            Program = message.Program
        });
    }
}
