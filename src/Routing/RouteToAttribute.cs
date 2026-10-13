namespace Routing;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RouteToAttribute(string destination) : Attribute
{
    public string Destination { get; } = destination;
}
