using Messages;
using Routing;
using NServiceBus;

namespace Transport;

public static class AttributeRouting
{
    public static void ApplyAttributeRoutes(this RoutingSettings routing)
    {
        foreach (var (message, destination) in MessageRoutes.Find(typeof(EnrollStudentCommand).Assembly))
        {
            routing.RouteToEndpoint(message, destination);
        }
    }
}
