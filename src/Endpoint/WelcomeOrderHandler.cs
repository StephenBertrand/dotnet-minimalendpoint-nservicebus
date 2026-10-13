using Enrollment;
using NServiceBus;

namespace EnrollmentEndpoint;

[Handler]
public sealed class WelcomeOrderHandler : IHandleMessages<EnrollmentCompletedEvent>
{
    public Task Handle(EnrollmentCompletedEvent message, IMessageHandlerContext context) =>
        context.SendLocal(new CreateWelcomeOrderCommand
        {
            TrackingId = message.TrackingId,
            Name = message.Name,
            Email = message.Email,
            Program = message.Program
        });
}
