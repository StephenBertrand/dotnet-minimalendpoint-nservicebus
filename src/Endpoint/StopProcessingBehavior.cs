using Enrollment;
using Microsoft.FeatureManagement;
using NServiceBus;
using NServiceBus.Pipeline;

namespace EnrollmentEndpoint;

/// <summary>
/// While StopProcessing is on, the handler does not run and the message is not failed.
/// The same message is scheduled back onto this endpoint 30 seconds later. The broker holds it.
/// </summary>
public sealed class StopProcessingBehavior(IFeatureManager features) : Behavior<IIncomingLogicalMessageContext>
{
    public const string HoldFor = "00:00:30";

    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        if (await features.IsEnabledAsync(EnrollmentFeatures.StopProcessing))
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
