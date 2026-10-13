namespace Routing;

/// <summary>
/// Endpoint that handles an event. Commands use <see cref="RouteToAttribute"/> instead.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HandleOnAttribute(string endpoint) : Attribute
{
    public string Endpoint { get; } = endpoint;
}
