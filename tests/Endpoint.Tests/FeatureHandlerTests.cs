using Endpoint.Extensions;
using EnrollmentEndpoint;
using Messages;
using Routing;
using NServiceBus;

namespace Endpoint.Tests;

public sealed class FeatureHandlerTests
{
    [Fact]
    public void Commands_declare_a_destination_and_events_do_not()
    {
        var routed = MessageRoutes.Find(typeof(EnrollStudentCommand).Assembly).ToArray();

        Assert.Contains(routed, route => route.Message == typeof(EnrollStudentCommand) && route.Destination == "Enrollment");
        Assert.Contains(routed, route => route.Message == typeof(CreateWelcomeOrderCommand) && route.Destination == "Orders");
        Assert.DoesNotContain(routed, route => route.Message == typeof(EnrollmentCompletedEvent));
        Assert.All(routed, route => Assert.True(MessageConventions.IsCommand(route.Message)));
    }

    [Fact]
    public void Handlers_bind_to_the_endpoint_named_on_the_message_or_the_class()
    {
        var enrollment = Assert.Single(FeatureHandlers.In(typeof(Enrollment.EnrollStudent.AutoConfigureServices)));
        Assert.Equal(typeof(Enrollment.EnrollStudent.EnrollStudentCommandHandler), enrollment.Handler);
        Assert.Equal("Enrollment", enrollment.Destination);

        var orders = FeatureHandlers.In(typeof(Orders.CreateWelcomeOrder.AutoConfigureServices));
        Assert.Contains(orders, binding => binding.Handler == typeof(Orders.CreateWelcomeOrder.EnrollmentCompletedEventHandler) && binding.Destination == "Orders");
        Assert.Contains(orders, binding => binding.Handler == typeof(Orders.CreateWelcomeOrder.CreateWelcomeOrderCommandHandler) && binding.Destination == "Orders");
        Assert.Empty(FeatureHandlers.In(typeof(Enrollment.Shared.AutoConfigureServices)));
        Assert.Empty(FeatureHandlers.In(typeof(Orders.Shared.AutoConfigureServices)));
    }

    [Fact]
    public void Discover_loads_feature_projects_the_compiler_left_unnamed()
    {
        var referenced = typeof(WorkerEndpoints).Assembly.GetReferencedAssemblies().Select(name => name.Name);

        Assert.DoesNotContain("Enrollment", referenced);
        Assert.DoesNotContain("Orders", referenced);

        var features = FeatureModules.Discover(typeof(WorkerEndpoints).Assembly).Select(feature => feature.GetType()).ToArray();

        Assert.Contains(typeof(Enrollment.EnrollStudent.AutoConfigureServices), features);
        Assert.Contains(typeof(Enrollment.Shared.AutoConfigureServices), features);
        Assert.Contains(typeof(Orders.CreateWelcomeOrder.AutoConfigureServices), features);
        Assert.Contains(typeof(Orders.Shared.AutoConfigureServices), features);
    }

    [Fact]
    public void Generated_registration_accepts_a_convention_handler()
    {
        var endpoint = new EndpointConfiguration("Orders");
        endpoint.AssemblyScanner().Disable = true;

        FeatureHandlers.AddFrom(typeof(Orders.CreateWelcomeOrder.AutoConfigureServices), endpoint, "Orders");
        FeatureHandlers.AddFrom(typeof(Enrollment.EnrollStudent.AutoConfigureServices), endpoint, "Orders");
    }
}
