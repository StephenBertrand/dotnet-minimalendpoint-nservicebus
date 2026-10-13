using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Endpoint.Extensions;

public static class HandlerLog
{
    public static IDisposable? BeginHandlerScope(this ILogger logger, string messageId, Guid trackingId) =>
        logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["HandlerProcessId"] = Guid.NewGuid(),
            ["TrackingId"] = trackingId
        });

    public static void LogMessageReceived<T>(this ILogger logger, T message) =>
        logger.LogInformation("MESSAGE-RECEIVED {MessageType} {Body}", typeof(T).Name, JsonSerializer.Serialize(message));

    public static void LogMessageSent<T>(this ILogger logger, T message) =>
        logger.LogInformation("MESSAGE-SENT {MessageType} {Body}", typeof(T).Name, JsonSerializer.Serialize(message));
}
