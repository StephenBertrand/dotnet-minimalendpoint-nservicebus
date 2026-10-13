using System.Text.Json;
using Api.Hosting;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Enrollment;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Features.Processing;

public sealed class ProcessingEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/processing").WithTags("Processing");
        group.MapGet("/", GetAsync).WithName("GetProcessing");
        group.MapPut("/", PutAsync).WithName("SetProcessing");
    }

    private static async Task<IResult> GetAsync(IProcessingSwitch processing, CancellationToken cancellationToken)
    {
        var catalog = await processing.ReadAsync(cancellationToken);
        return Results.Ok(catalog);
    }

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

        if (!TryResolve(update, out var endpoint, out var message, out var mode, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [error.Key] = [error.Message]
            });
        }

        await processing.SetAsync(endpoint, message, mode, cancellationToken);
        return Results.Ok(await processing.ReadAsync(cancellationToken));
    }

    private static bool TryResolve(
        ProcessingUpdate update,
        out string endpoint,
        out string? message,
        out string mode,
        out (string Key, string Message) error)
    {
        endpoint = "";
        message = null;
        mode = "";
        error = default;

        var match = QueueGate.Endpoints.FirstOrDefault(item =>
            item.Name.Equals(update.Endpoint, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            error = ("endpoint", "Choose Enrollment or Orders.");
            return false;
        }

        endpoint = match.Name;
        if (!string.IsNullOrWhiteSpace(update.Message))
        {
            var handler = match.Handlers.FirstOrDefault(item =>
                item.Message.Equals(update.Message, StringComparison.OrdinalIgnoreCase));
            if (handler is null)
            {
                error = ("message", $"That message is not handled by {match.Name}.");
                return false;
            }

            message = handler.Message;
        }

        var requested = update.Mode?.Trim().ToLowerInvariant();
        if (requested is not (QueueGate.Running or QueueGate.Pause or QueueGate.Error))
        {
            error = ("mode", "Mode must be running, pause, or error.");
            return false;
        }

        if (requested == QueueGate.Pause && !QueueGate.CanPause(endpoint, message))
        {
            error = ("mode", "Pause holds commands. Events keep processing.");
            return false;
        }

        mode = requested;
        return true;
    }
}

public sealed record ProcessingCatalog(bool CanUpdate, ProcessingEndpointState[] Endpoints);

public sealed record ProcessingEndpointState(string Name, string Label, string Mode, ProcessingHandlerState[] Handlers);

public sealed record ProcessingHandlerState(string Message, string Label, string Mode);

public sealed record ProcessingUpdate(string? Endpoint, string? Message, string? Mode);

public interface IProcessingSwitch
{
    bool CanUpdate { get; }

    Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken);

    Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken);
}

public static class ProcessingSwitchHosting
{
    public static TBuilder AddProcessingSwitch<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (TryCreateAnonymousStore(builder.Configuration, out var endpoint))
        {
            builder.Services.AddSingleton<IProcessingSwitch>(_ => new AnonymousProcessingSwitch(endpoint));
            return builder;
        }

        var client = CreateClient(builder.Configuration);
        if (client is null)
        {
            builder.Services.AddSingleton<IProcessingSwitch, UnavailableProcessingSwitch>();
            return builder;
        }

        builder.Services.AddSingleton(client);
        builder.Services.AddSingleton<IProcessingSwitch, AppConfigurationProcessingSwitch>();
        return builder;
    }

    private static bool TryCreateAnonymousStore(IConfiguration configuration, out Uri endpoint)
    {
        endpoint = null!;
        var connectionString = configuration.GetConnectionString(AzureAppConfigurationHosting.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString)
            || !connectionString.Contains("Anonymous=true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (part[..separator].Equals("Endpoint", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(part[(separator + 1)..], UriKind.Absolute, out var parsed))
            {
                endpoint = parsed;
                return true;
            }
        }

        return false;
    }

    private static ConfigurationClient? CreateClient(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AzureAppConfigurationHosting.ConnectionName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (connectionString.Contains("Secret=", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Id=", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigurationClient(connectionString);
            }

            if (Uri.TryCreate(connectionString, UriKind.Absolute, out var fromConnection))
            {
                return new ConfigurationClient(fromConnection, new DefaultAzureCredential());
            }
        }

        var endpoint = configuration["Aspire:Microsoft:Extensions:Configuration:AzureAppConfiguration:Endpoint"]
            ?? configuration[$"{AzureAppConfigurationHosting.ConnectionName}:endpoint"];
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var fromEndpoint))
        {
            return new ConfigurationClient(fromEndpoint, new DefaultAzureCredential());
        }

        return null;
    }
}

sealed class UnavailableProcessingSwitch : IProcessingSwitch
{
    public bool CanUpdate => false;

    public Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ProcessingCatalogReader.Running(canUpdate: false));

    public Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App Configuration is not configured.");
}

static class ProcessingCatalogReader
{
    public static ProcessingCatalog Running(bool canUpdate) =>
        new(canUpdate, QueueGate.Endpoints.Select(endpoint => new ProcessingEndpointState(
            endpoint.Name,
            endpoint.Label,
            QueueGate.Running,
            endpoint.Handlers.Select(handler => new ProcessingHandlerState(handler.Message, handler.Label, QueueGate.Running)).ToArray()
        )).ToArray());

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

sealed class AnonymousProcessingSwitch : IProcessingSwitch
{
    private const string ApiVersion = "2023-11-01";
    private const string KvContentType = "application/vnd.microsoft.appconfig.kv+json";
    private readonly HttpClient _client;

    public AnonymousProcessingSwitch(Uri endpoint)
    {
        _client = new HttpClient { BaseAddress = endpoint };
    }

    public bool CanUpdate => true;

    public async Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in Keys())
        {
            values[key] = await ReadRawAsync(key, cancellationToken);
        }

        return ProcessingCatalogReader.From(true, key => values.GetValueOrDefault(key) ?? "");
    }

    public async Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken)
    {
        var key = message is null
            ? QueueGate.EndpointKey(endpoint)
            : QueueGate.HandlerKey(endpoint, message);
        await PutAsync(key, mode, cancellationToken);

        var sentinel = await ReadRawAsync(AzureAppConfigurationHosting.SentinelKey, cancellationToken);
        var next = int.TryParse(sentinel, out var number) ? (number + 1).ToString() : "1";
        await PutAsync(AzureAppConfigurationHosting.SentinelKey, next, cancellationToken);
    }

    private async Task<string?> ReadRawAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(KeyPath(key), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var document = await System.Text.Json.Nodes.JsonNode.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return document?["value"]?.GetValue<string>();
    }

    private async Task PutAsync(string key, string value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { value });
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, KvContentType);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(KvContentType);
        using var response = await _client.PutAsync(KeyPath(key), content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string KeyPath(string key) =>
        $"/kv/{Uri.EscapeDataString(key)}?api-version={ApiVersion}";

    private static IEnumerable<string> Keys()
    {
        foreach (var endpoint in QueueGate.Endpoints)
        {
            yield return QueueGate.EndpointKey(endpoint.Name);
            foreach (var handler in endpoint.Handlers)
            {
                yield return QueueGate.HandlerKey(endpoint.Name, handler.Message);
            }
        }
    }
}

sealed class AppConfigurationProcessingSwitch(ConfigurationClient client) : IProcessingSwitch
{
    public bool CanUpdate => true;

    public async Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var endpoint in QueueGate.Endpoints)
        {
            var endpointKey = QueueGate.EndpointKey(endpoint.Name);
            values[endpointKey] = await ReadAsync(endpointKey, cancellationToken);
            foreach (var handler in endpoint.Handlers)
            {
                var handlerKey = QueueGate.HandlerKey(endpoint.Name, handler.Message);
                values[handlerKey] = await ReadAsync(handlerKey, cancellationToken);
            }
        }

        return ProcessingCatalogReader.From(true, key => values.GetValueOrDefault(key) ?? "");
    }

    public async Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken)
    {
        var key = message is null
            ? QueueGate.EndpointKey(endpoint)
            : QueueGate.HandlerKey(endpoint, message);
        await client.SetConfigurationSettingAsync(new ConfigurationSetting(key, mode), cancellationToken: cancellationToken);
        await BumpSentinelAsync(cancellationToken);
    }

    private async Task<string?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await client.GetConfigurationSettingAsync(key, cancellationToken: cancellationToken);
            return setting.Value.Value;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    private async Task BumpSentinelAsync(CancellationToken cancellationToken)
    {
        var key = AzureAppConfigurationHosting.SentinelKey;
        string next = "1";
        try
        {
            var current = await client.GetConfigurationSettingAsync(key, cancellationToken: cancellationToken);
            next = int.TryParse(current.Value.Value, out var number)
                ? (number + 1).ToString()
                : "1";
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            next = "1";
        }

        await client.SetConfigurationSettingAsync(new ConfigurationSetting(key, next), cancellationToken: cancellationToken);
    }
}
