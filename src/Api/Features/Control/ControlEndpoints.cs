using Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Features.Control;

public sealed class ControlEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/control").WithTags("Control");
        group.MapGet("/", GetAsync).WithName("GetControlRoom");
        group.MapGet("/activity", ActivityAsync).WithName("GetControlActivity");
        group.MapGet("/failures", FailuresAsync).WithName("ListControlFailures");
        group.MapGet("/failures/{id:guid}", FailureAsync).WithName("GetControlFailure");
        group.MapPost("/failures/{id:guid}/retry", (Guid id, IControlRoom room, CancellationToken cancellationToken) =>
            ActAsync(room, () => room.PostFailureAsync(id.ToString("D"), "retry", cancellationToken))).WithName("RetryControlFailure");
        group.MapPost("/failures/{id:guid}/archive", (Guid id, IControlRoom room, CancellationToken cancellationToken) =>
            ActAsync(room, () => room.PostFailureAsync(id.ToString("D"), "archive", cancellationToken))).WithName("ArchiveControlFailure");
        group.MapGet("/groups", GroupsAsync).WithName("ListControlGroups");
        group.MapPost("/groups/retry", (GroupAction action, IControlRoom room, CancellationToken cancellationToken) =>
            ActGroupAsync(room, action, "retry", cancellationToken)).WithName("RetryControlGroup");
        group.MapPost("/groups/archive", (GroupAction action, IControlRoom room, CancellationToken cancellationToken) =>
            ActGroupAsync(room, action, "archive", cancellationToken)).WithName("ArchiveControlGroup");
        group.MapGet("/traffic", TrafficAsync).WithName("GetControlTraffic");
    }

    private static readonly int[] HistoryWindows = [1, 5, 10, 15, 30, 60];

    private static async Task<IResult> GetAsync(IControlRoom room, CancellationToken cancellationToken) =>
        Results.Ok(await room.ReadAsync(cancellationToken));

    private static async Task<IResult> ActivityAsync(int? history, IControlRoom room, CancellationToken cancellationToken)
    {
        var window = history is int value && HistoryWindows.Contains(value) ? value : 1;
        return Results.Ok(await room.ActivityAsync(window, cancellationToken));
    }

    private static async Task<IResult> FailuresAsync(string? status, IControlRoom room, CancellationToken cancellationToken)
    {
        var selected = status is "archived" or "unresolved" ? status : "unresolved";
        if (!room.IsConfigured)
        {
            return Results.Ok(new FailureList(0, []));
        }

        return Results.Ok(await room.FailuresAsync(selected, cancellationToken));
    }

    private static async Task<IResult> FailureAsync(Guid id, IControlRoom room, CancellationToken cancellationToken)
    {
        if (!room.IsConfigured)
        {
            return Unavailable();
        }

        var detail = await room.FailureAsync(id.ToString("D"), cancellationToken);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> GroupsAsync(string? classifier, IControlRoom room, CancellationToken cancellationToken) =>
        Results.Ok(await room.GroupsAsync(classifier ?? "Exception Type and Stack Trace", cancellationToken));

    private static async Task<IResult> TrafficAsync(string? endpoint, IControlRoom room, CancellationToken cancellationToken) =>
        Results.Ok(await room.TrafficAsync(string.IsNullOrWhiteSpace(endpoint) ? "Enrollment" : endpoint, cancellationToken));

    private static async Task<IResult> ActAsync(IControlRoom room, Func<Task<bool>> action)
    {
        if (!room.IsConfigured)
        {
            return Unavailable();
        }

        var accepted = await action();
        return accepted
            ? Results.Accepted(value: new ControlActionResult(true))
            : Results.Problem("ServiceControl did not accept the action.", statusCode: StatusCodes.Status502BadGateway);
    }

    private static Task<IResult> ActGroupAsync(
        IControlRoom room,
        GroupAction action,
        string verb,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Id))
        {
            return Task.FromResult(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["A failure group id is required."]
            }));
        }

        return ActAsync(room, () => room.PostGroupAsync(action.Id, verb, cancellationToken));
    }

    private static IResult Unavailable() =>
        Results.Problem(
            "ServiceControl is not referenced by this API.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
}

public static class ControlRoomHosting
{
    public static TBuilder AddControlRoom<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        ServiceControlRoom.ConfigureClient(builder, ServiceControlRoom.ErrorClient, "particular-error");
        ServiceControlRoom.ConfigureClient(builder, ServiceControlRoom.AuditClient, "particular-audit");
        ServiceControlRoom.ConfigureClient(builder, ServiceControlRoom.MonitoringClient, "particular-monitoring");
        builder.Services.AddSingleton<IControlRoom, ServiceControlRoom>();
        return builder;
    }
}
