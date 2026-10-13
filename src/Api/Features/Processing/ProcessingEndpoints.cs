using Api.Hosting;
using Endpoint.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.Features.Processing;

public sealed class ProcessingEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/processing").WithTags("Processing");
        group.MapGet("/", GetAsync).WithName("GetProcessing");
        group.MapPut("/", PutAsync).WithName("SetProcessing");
    }

    private static async Task<IResult> GetAsync(IProcessingSwitch processing, CancellationToken cancellationToken) =>
        Results.Ok(await processing.ReadAsync(cancellationToken));

    private static async Task<IResult> PutAsync(
        ProcessingUpdate update,
        IProcessingSwitch processing,
        CancellationToken cancellationToken)
    {
        if (!processing.CanUpdate)
        {
            return Results.Problem(
                "App Configuration is not configured, so queue gates cannot be changed.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        switch (ProcessingBinding.Bind(update))
        {
            case ProcessingRejection rejection:
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [rejection.Key] = [rejection.Message]
                });
            case ProcessingAcceptance acceptance:
                await processing.SetAsync(acceptance.Endpoint, acceptance.Message, acceptance.Mode, cancellationToken);
                return Results.Ok(await processing.ReadAsync(cancellationToken));
            default:
                throw new InvalidOperationException("Queue gate update was neither accepted nor rejected.");
        }
    }
}

public sealed record ProcessingUpdate(string? Endpoint, string? Message, string? Mode);

abstract record ProcessingBinding
{
    public static ProcessingBinding Bind(ProcessingUpdate update)
    {
        var match = QueueGate.Endpoints.FirstOrDefault(item =>
            item.Name.Equals(update.Endpoint, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            var names = string.Join(" or ", QueueGate.Endpoints.Select(item => item.Name));
            return new ProcessingRejection("endpoint", $"Choose {names}.");
        }

        string? message = null;
        if (!string.IsNullOrWhiteSpace(update.Message))
        {
            var handler = match.Handlers.FirstOrDefault(item =>
                item.Message.Equals(update.Message, StringComparison.OrdinalIgnoreCase));
            if (handler is null)
            {
                return new ProcessingRejection("message", $"That message is not handled by {match.Name}.");
            }

            message = handler.Message;
        }

        var mode = update.Mode?.Trim().ToLowerInvariant();
        if (mode is not (QueueGate.Running or QueueGate.Pause or QueueGate.Error))
        {
            return new ProcessingRejection("mode", "Mode must be running, pause, or error.");
        }

        if (mode == QueueGate.Pause && !QueueGate.CanPause(match.Name, message))
        {
            return new ProcessingRejection("mode", "Pause holds commands. Events keep processing.");
        }

        return new ProcessingAcceptance(match.Name, message, mode);
    }
}

sealed record ProcessingAcceptance(string Endpoint, string? Message, string Mode) : ProcessingBinding;

sealed record ProcessingRejection(string Key, string Message) : ProcessingBinding;
