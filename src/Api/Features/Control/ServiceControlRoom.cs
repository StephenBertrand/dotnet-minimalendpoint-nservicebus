using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Api.Features.Control;

public interface IControlRoom
{
    bool IsConfigured { get; }

    bool AuditConfigured { get; }

    Task<ControlSnapshot> ReadAsync(CancellationToken cancellationToken);

    Task<ActivityPage> ActivityAsync(int history, CancellationToken cancellationToken);

    Task<FailureList> FailuresAsync(string status, CancellationToken cancellationToken);

    Task<FailureDetail?> FailureAsync(string id, CancellationToken cancellationToken);

    Task<GroupList> GroupsAsync(string classifier, CancellationToken cancellationToken);

    Task<TrafficList> TrafficAsync(string endpoint, CancellationToken cancellationToken);

    Task<bool> PostFailureAsync(string id, string action, CancellationToken cancellationToken);

    Task<bool> PostGroupAsync(string id, string action, CancellationToken cancellationToken);
}

public sealed class ServiceControlRoom(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IControlRoom
{
    public const string ErrorClient = "particular-error";
    public const string AuditClient = "particular-audit";
    public const string MonitoringClient = "particular-monitoring";

    private static readonly HashSet<string> Classifiers = new(StringComparer.Ordinal)
    {
        "Exception Type and Stack Trace",
        "Message Type",
        "Endpoint Address",
        "Endpoint Instance",
        "Endpoint Name"
    };

    public bool IsConfigured { get; } = AbsoluteUrl(configuration, "particular-error") is not null;

    public bool AuditConfigured { get; } = AbsoluteUrl(configuration, "particular-audit") is not null;

    public bool MonitoringConfigured { get; } = AbsoluteUrl(configuration, "particular-monitoring") is not null;

    public async Task<ControlSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ControlReader.Empty("unconfigured", "ServiceControl is not referenced by this API.");
        }

        try
        {
            var snapshot = await ReadErrorInstanceAsync(cancellationToken);
            return MonitoringConfigured
                ? await WithMonitoringAsync(snapshot, historyMinutes: 1, cancellationToken)
                : snapshot;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsLinkDown(exception))
        {
            return ControlReader.Empty("down", "ServiceControl did not answer.");
        }
    }

    async Task<ControlSnapshot> ReadErrorInstanceAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ErrorClient);
        var root = GetAsync(client, "/api", cancellationToken);
        var heartbeats = GetAsync(client, "/api/heartbeats/stats", cancellationToken);
        var endpoints = GetAsync(client, "/api/endpoints", cancellationToken);
        var unresolved = GetCountAsync(client, "/api/errors?status=unresolved&page=1&per_page=1", cancellationToken);
        var checks = GetAsync(client, "/api/customchecks", cancellationToken);
        var events = GetAsync(client, "/api/eventlogitems", cancellationToken);
        await Task.WhenAll(root, heartbeats, endpoints, unresolved, checks, events);

        using var rootDocument = root.Result;
        using var heartbeatDocument = heartbeats.Result;
        using var endpointDocument = endpoints.Result;
        using var checkDocument = checks.Result;
        using var eventDocument = events.Result;

        var snapshot = ControlReader.ReadSnapshot(
            License(rootDocument),
            heartbeatDocument?.RootElement ?? default,
            unresolved.Result,
            checkDocument?.RootElement ?? default,
            eventDocument?.RootElement ?? default);

        return WithEndpointRows(snapshot, ControlReader.ReadEndpoints(endpointDocument?.RootElement ?? default));
    }

    static ControlSnapshot WithEndpointRows(ControlSnapshot snapshot, ControlEndpoint[] rows)
    {
        if (rows.Length == 0)
        {
            return snapshot;
        }

        return snapshot with
        {
            Endpoints = rows,
            HeartbeatsActive = rows.Count(row => row.Status == "beating"),
            HeartbeatsFailing = rows.Count(row => row.Status is "dead" or "down")
        };
    }

    async Task<ControlSnapshot> WithMonitoringAsync(
        ControlSnapshot snapshot,
        int historyMinutes,
        CancellationToken cancellationToken)
    {
        var reading = await ReadMonitoringAsync(historyMinutes, cancellationToken);
        return snapshot with
        {
            Monitoring = reading.State,
            MonitoringNote = reading.Note,
            QueueLength = reading.QueueLength,
            Activity = reading.Activity
        };
    }

    public async Task<ActivityPage> ActivityAsync(int history, CancellationToken cancellationToken)
    {
        if (!MonitoringConfigured)
        {
            return new ActivityPage(history, "dark", ControlReader.MonitoringDark, []);
        }

        var reading = await ReadMonitoringAsync(history, cancellationToken);
        return new ActivityPage(history, reading.State, reading.Note, reading.Activity);
    }

    private async Task<ControlReader.MonitoringReading> ReadMonitoringAsync(int history, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(MonitoringClient);
            using var document = await GetAsync(client, $"/monitored-endpoints?history={history}", cancellationToken);
            if (document is null)
            {
                return new ControlReader.MonitoringReading("down", "Monitoring did not answer.", null, []);
            }

            return ControlReader.ReadMonitoring(document.RootElement);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsLinkDown(exception))
        {
            return new ControlReader.MonitoringReading("down", "Monitoring did not answer.", null, []);
        }
    }

    private static bool IsLinkDown(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or JsonException
        || (exception is AggregateException aggregate && aggregate.InnerExceptions.All(IsLinkDown));

    public async Task<FailureList> FailuresAsync(string status, CancellationToken cancellationToken)
    {
        if (!IsConfigured || status is not ("unresolved" or "archived"))
        {
            return new FailureList(0, []);
        }

        var client = httpClientFactory.CreateClient(ErrorClient);
        var (document, total) = await GetCountedAsync(
            client,
            $"/api/errors?status={status}&page=1&per_page=40&sort=time_of_failure&direction=desc",
            cancellationToken);
        using (document)
        {
            var messages = ControlReader.ReadFailures(document?.RootElement ?? default);
            return new FailureList(total, messages);
        }
    }

    public async Task<FailureDetail?> FailureAsync(string id, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(ErrorClient);
        using var document = await GetAsync(client, $"/api/errors/{Uri.EscapeDataString(id)}", cancellationToken);
        if (document is null)
        {
            return null;
        }

        var path = ControlReader.BodyPath(document.RootElement);
        string? body = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            body = await GetTextAsync(client, path, cancellationToken);
            if (body is { Length: > 8000 })
            {
                body = body[..8000] + "\n…";
            }
        }

        return ControlReader.ReadDetail(document.RootElement, body);
    }

    public async Task<GroupList> GroupsAsync(string classifier, CancellationToken cancellationToken)
    {
        if (!Classifiers.Contains(classifier))
        {
            classifier = "Exception Type and Stack Trace";
        }

        if (!IsConfigured)
        {
            return new GroupList(classifier, []);
        }

        var client = httpClientFactory.CreateClient(ErrorClient);
        using var document = await GetAsync(
            client,
            "/api/recoverability/groups/" + Uri.EscapeDataString(classifier),
            cancellationToken);
        return new GroupList(classifier, ControlReader.ReadGroups(document?.RootElement ?? default));
    }

    public async Task<TrafficList> TrafficAsync(string endpoint, CancellationToken cancellationToken)
    {
        if (!AllowedEndpoint(endpoint))
        {
            return new TrafficList(false, endpoint, "Unknown endpoint.", []);
        }

        if (!AuditConfigured)
        {
            return new TrafficList(false, endpoint, "The audit instance is not referenced.", []);
        }

        var client = httpClientFactory.CreateClient(AuditClient);
        using var document = await GetAsync(
            client,
            $"/api/endpoints/{Uri.EscapeDataString(endpoint)}/messages?page=1&per_page=20",
            cancellationToken);
        if (document is null)
        {
            return new TrafficList(false, endpoint, "The audit instance did not answer.", []);
        }

        return new TrafficList(true, endpoint, null, ControlReader.ReadTraffic(document.RootElement));
    }

    public Task<bool> PostFailureAsync(string id, string action, CancellationToken cancellationToken)
    {
        if (!IsConfigured || action is not ("retry" or "archive"))
        {
            return Task.FromResult(false);
        }

        var client = httpClientFactory.CreateClient(ErrorClient);
        return PostAsync(client, $"/api/errors/{Uri.EscapeDataString(id)}/{action}", cancellationToken);
    }

    public Task<bool> PostGroupAsync(string id, string action, CancellationToken cancellationToken)
    {
        if (!IsConfigured || action is not ("retry" or "archive") || !AllowedGroupId(id))
        {
            return Task.FromResult(false);
        }

        var path = string.Join('/', id.Split('/').Select(Uri.EscapeDataString));
        var client = httpClientFactory.CreateClient(ErrorClient);
        return PostAsync(client, $"/api/recoverability/groups/{path}/errors/{action}", cancellationToken);
    }

    public static string? AbsoluteUrl(IConfiguration configuration, string resource)
    {
        var direct = configuration[$"ServiceControl:{resource}"];
        if (Uri.TryCreate(direct, UriKind.Absolute, out var directUri) && directUri.Scheme == Uri.UriSchemeHttp)
        {
            return directUri.ToString();
        }

        var discovered = configuration[$"services:{resource}:http:0"];
        if (Uri.TryCreate(discovered, UriKind.Absolute, out var discoveredUri)
            && (discoveredUri.Scheme == Uri.UriSchemeHttp || discoveredUri.Scheme == Uri.UriSchemeHttps))
        {
            return discoveredUri.ToString();
        }

        return null;
    }

    public static void ConfigureClient(IHostApplicationBuilder builder, string name, string resource)
    {
        var url = AbsoluteUrl(builder.Configuration, resource);
#pragma warning disable EXTEXP0001 // A failed retry or archive must not be posted twice.
        builder.Services.AddHttpClient(name, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            if (url is not null)
            {
                client.BaseAddress = new Uri(url);
            }
        }).RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
    }

    private static bool AllowedEndpoint(string endpoint) =>
        endpoint.Length is > 0 and <= 80 && endpoint.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static bool AllowedGroupId(string id) =>
        id.Length is > 0 and <= 400 && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '/' or '-');

    private static string? License(JsonDocument? document)
    {
        if (document is null || !document.RootElement.TryGetProperty("license_status", out var status))
        {
            return null;
        }

        return status.ValueKind == JsonValueKind.String ? status.GetString() : null;
    }

    private static async Task<JsonDocument?> GetAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task<int> GetCountAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var fallback = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.GetArrayLength()
            : 0;
        return ControlReader.ReadTotal(response, fallback);
    }

    private static async Task<(JsonDocument? Document, int Total)> GetCountedAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, 0);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var fallback = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.GetArrayLength()
            : 0;
        return (document, ControlReader.ReadTotal(response, fallback));
    }

    private static async Task<string?> GetTextAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task<bool> PostAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(path, new { }, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
