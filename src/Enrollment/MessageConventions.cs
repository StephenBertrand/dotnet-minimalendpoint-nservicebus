namespace Enrollment;

public static class MessageConventions
{
    public static bool IsCommand(Type type) => IsCommandName(type.Name);

    public static bool IsEvent(Type type) => IsEventName(type.Name);

    public static bool IsCommandName(string name) =>
        name.EndsWith("Command", StringComparison.Ordinal);

    public static bool IsEventName(string name) =>
        name.EndsWith("Event", StringComparison.Ordinal);
}
