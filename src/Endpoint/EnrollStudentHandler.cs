using Enrollment;
using NServiceBus;

namespace EnrollmentEndpoint;

public sealed class EnrollStudentHandler(EnrollmentProcessor processor) : IHandleMessages<EnrollStudent>
{
    public Task Handle(EnrollStudent message, IMessageHandlerContext context) =>
        processor.ProcessAsync(message, context.CancellationToken);
}
