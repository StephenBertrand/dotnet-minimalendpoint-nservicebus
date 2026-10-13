using Microsoft.Extensions.Configuration;
using Routing;

namespace Endpoint.Extensions;

public static class QueueGate
{
    public const string Running = "running";
    public const string Pause = "pause";
    public const string Error = "error";
    public const string KeyPrefix = "Demo:Processing:";

    public const string EnrollmentEndpoint = "Enrollment";
    public const string OrdersEndpoint = "Orders";

    public static readonly QueueGateEndpoint[] Endpoints =
    [
        new(EnrollmentEndpoint, "Enrollment endpoint",
        [
            new("EnrollStudentCommand", "Enroll student")
        ]),
        new(OrdersEndpoint, "Orders endpoint",
        [
            new("EnrollmentCompletedEvent", "Welcome order event"),
            new("CreateWelcomeOrderCommand", "Write welcome order")
        ])
    ];

    public static string EndpointKey(string endpoint) => KeyPrefix + endpoint;

    public static string HandlerKey(string endpoint, string message) => $"{KeyPrefix}{endpoint}:{message}";

    public static string Key(string endpoint, string? message) =>
        message is null ? EndpointKey(endpoint) : HandlerKey(endpoint, message);

    public static IEnumerable<string> Keys()
    {
        foreach (var endpoint in Endpoints)
        {
            yield return EndpointKey(endpoint.Name);
            foreach (var handler in endpoint.Handlers)
            {
                yield return HandlerKey(endpoint.Name, handler.Message);
            }
        }
    }

    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            Pause => Pause,
            Error => Error,
            _ => Running
        };

    public static string Effective(IConfiguration configuration, string endpoint, string message)
    {
        var endpointMode = Normalize(configuration[EndpointKey(endpoint)]);
        if (endpointMode != Running)
        {
            return endpointMode;
        }

        return Normalize(configuration[HandlerKey(endpoint, message)]);
    }

    public static bool Pauses(string mode, string messageName) =>
        mode == Pause && MessageConventions.IsCommandName(messageName);

    public static bool CanPause(string endpoint, string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return MessageConventions.IsCommandName(message);
        }

        var match = Endpoints.FirstOrDefault(item =>
            item.Name.Equals(endpoint, StringComparison.OrdinalIgnoreCase));
        return match?.Handlers.Any(handler => MessageConventions.IsCommandName(handler.Message)) == true;
    }
}

public sealed record QueueGateEndpoint(string Name, string Label, QueueGateHandler[] Handlers);

public sealed record QueueGateHandler(string Message, string Label);
