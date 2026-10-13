using System.Reflection;
using Routing;

namespace Endpoint.Extensions;

public sealed record HandlerBinding(Type Handler, string Destination, IReadOnlyList<Type> Messages);

public static class FeatureHandlers
{
    const string HandlerAttributeName = "NServiceBus.HandlerAttribute";

    public static IReadOnlyList<HandlerBinding> Scan(Assembly assembly) =>
        assembly.GetTypes().Where(IsHandler).Select(Bind).ToArray();

    public static IReadOnlyList<HandlerBinding> In(Type feature) =>
        Scan(feature.Assembly).Where(binding => BelongsTo(binding.Handler, feature)).ToArray();

    public static void AddFrom(Type feature, object endpointConfiguration, string endpointName)
    {
        foreach (var binding in In(feature))
        {
            if (!string.Equals(binding.Destination, endpointName, StringComparison.Ordinal))
            {
                continue;
            }

            Register(feature.Assembly, endpointConfiguration, binding.Handler);
        }
    }

    static bool BelongsTo(Type handler, Type feature)
    {
        var featureNamespace = feature.Namespace;
        var handlerNamespace = handler.Namespace;
        if (string.IsNullOrEmpty(featureNamespace) || handlerNamespace is null)
        {
            return false;
        }

        return handlerNamespace == featureNamespace
            || handlerNamespace.StartsWith(featureNamespace + ".", StringComparison.Ordinal);
    }

    static void Register(Assembly assembly, object endpointConfiguration, Type handler)
    {
        var methodName = "Add" + handler.Name;
        var method = FindGeneratedAddMethod(assembly, methodName);
        if (method?.DeclaringType is null)
        {
            throw new InvalidOperationException(
                $"NServiceBus did not generate {methodName} for {handler.FullName}. Mark the class with [Handler] and build the feature project.");
        }

        var registry = Activator.CreateInstance(method.DeclaringType, endpointConfiguration)
            ?? throw new InvalidOperationException($"Could not create {method.DeclaringType.FullName}.");
        method.Invoke(registry, null);
    }

    static MethodInfo? FindGeneratedAddMethod(Assembly assembly, string methodName)
    {
        MethodInfo? method = null;
        foreach (var type in assembly.GetTypes())
        {
            var candidate = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);
            if (candidate is null)
            {
                continue;
            }

            if (method is not null)
            {
                throw new InvalidOperationException($"More than one generated registration method is named {methodName}.");
            }

            method = candidate;
        }

        return method;
    }

    static HandlerBinding Bind(Type handler)
    {
        var messages = MessagesHandledBy(handler);
        if (messages.Length == 0)
        {
            throw new InvalidOperationException($"{handler.FullName} is marked [Handler] and has no Handle method.");
        }

        var destinations = messages
            .Select(message => DestinationFor(handler, message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (destinations.Length != 1)
        {
            throw new InvalidOperationException(
                $"{handler.FullName} handles messages for more than one endpoint ({string.Join(", ", destinations)}). Split it into one class per endpoint.");
        }

        return new HandlerBinding(handler, destinations[0], messages);
    }

    static Type[] MessagesHandledBy(Type handler) =>
        handler.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Handle" && method.GetParameters().Length > 0)
            .Select(method => method.GetParameters()[0].ParameterType)
            .Distinct()
            .ToArray();

    static string DestinationFor(Type handler, Type message)
    {
        var route = message.GetCustomAttribute<RouteToAttribute>()?.Destination;
        var handleOn = handler.GetCustomAttribute<HandleOnAttribute>()?.Endpoint;
        if (MessageConventions.IsCommand(message))
        {
            return CommandDestination(handler, message, route, handleOn);
        }

        if (MessageConventions.IsEvent(message))
        {
            return EventDestination(handler, message, route, handleOn);
        }

        throw new InvalidOperationException($"{message.FullName} is not a command or an event.");
    }

    static string CommandDestination(Type handler, Type message, string? route, string? handleOn)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            throw new InvalidOperationException($"{message.FullName} has no {nameof(RouteToAttribute)}.");
        }

        if (handleOn is not null && !string.Equals(handleOn, route, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{handler.FullName} says HandleOn(\"{handleOn}\") but {message.Name} routes to \"{route}\".");
        }

        return route;
    }

    static string EventDestination(Type handler, Type message, string? route, string? handleOn)
    {
        if (route is not null)
        {
            throw new InvalidOperationException($"{message.FullName} is an event and must not declare {nameof(RouteToAttribute)}.");
        }

        if (string.IsNullOrWhiteSpace(handleOn))
        {
            throw new InvalidOperationException($"{handler.FullName} handles {message.Name} and has no {nameof(HandleOnAttribute)}.");
        }

        return handleOn;
    }

    static bool IsHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false } &&
        type.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == HandlerAttributeName);
}
