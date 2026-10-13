using Endpoint.Extensions;

namespace Api.Features.Processing;

public sealed record ProcessingCatalog(bool CanUpdate, ProcessingEndpointState[] Endpoints);

public sealed record ProcessingEndpointState(string Name, string Label, string Mode, ProcessingHandlerState[] Handlers);

public sealed record ProcessingHandlerState(string Message, string Label, string Mode);

static class ProcessingCatalogReader
{
    public static ProcessingCatalog Running(bool canUpdate) =>
        From(canUpdate, _ => QueueGate.Running);

    public static ProcessingCatalog From(bool canUpdate, Func<string, string> read) =>
        new(canUpdate, QueueGate.Endpoints.Select(endpoint => new ProcessingEndpointState(
            endpoint.Name,
            endpoint.Label,
            QueueGate.Normalize(read(QueueGate.EndpointKey(endpoint.Name))),
            endpoint.Handlers.Select(handler => new ProcessingHandlerState(
                handler.Message,
                handler.Label,
                QueueGate.Normalize(read(QueueGate.HandlerKey(endpoint.Name, handler.Message))))).ToArray()
        )).ToArray());
}

static class Sentinel
{
    public static string Next(string? current) =>
        int.TryParse(current, out var number) ? (number + 1).ToString() : "1";
}
