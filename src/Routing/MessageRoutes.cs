using System.Reflection;

namespace Routing;

public static class MessageRoutes
{
    public static IEnumerable<(Type Message, string Destination)> Find(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            var route = type.GetCustomAttribute<RouteToAttribute>();
            if (route is null)
            {
                continue;
            }

            if (!MessageConventions.IsCommand(type))
            {
                throw new InvalidOperationException(
                    $"{type.FullName} has {nameof(RouteToAttribute)}, and only commands declare a send route.");
            }

            if (string.IsNullOrWhiteSpace(route.Destination))
            {
                throw new InvalidOperationException($"{type.FullName} has an empty {nameof(RouteToAttribute)}.");
            }

            yield return (type, route.Destination);
        }
    }
}
