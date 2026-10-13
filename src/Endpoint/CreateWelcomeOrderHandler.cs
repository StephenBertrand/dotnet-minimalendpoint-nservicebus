using Enrollment;
using NServiceBus;

namespace EnrollmentEndpoint;

[Handler]
public sealed class CreateWelcomeOrderHandler(OrderProcessor processor) : IHandleMessages<CreateWelcomeOrderCommand>
{
    public Task Handle(CreateWelcomeOrderCommand message, IMessageHandlerContext context) =>
        processor.CreateWelcomeOrderAsync(message, context.CancellationToken);
}
