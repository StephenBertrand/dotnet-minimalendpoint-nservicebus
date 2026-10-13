using System.Text.Json;

namespace Api.Features.Control;

public static class ControlReader
{
    public const string MonitoringDark =
        "Queue length is dark. The monitoring instance is not referenced.";

    public static ControlSnapshot Empty(string link, string? note) => new(
        Available: false,
        Link: link,
        Note: note,
        License: null,
        Monitoring: "dark",
        MonitoringNote: MonitoringDark,
        HeartbeatsActive: 0,
        HeartbeatsFailing: 0,
        Unresolved: 0,
        ChecksPassing: 0,
        ChecksFailing: 0,
        Endpoints: [],
        Checks: [],
        Events: [],
        QueueLength: null,
        Activity: []);

    public static ControlSnapshot ReadSnapshot(
        string? license,
        JsonElement heartbeats,
        int unresolvedTotal,
        JsonElement checks,
        JsonElement events)
    {
        var endpoints = ReadEndpoints(heartbeats.ValueKind == JsonValueKind.Array
            ? heartbeats
            : default);
        var checkRows = ReadChecks(checks);
        var eventRows = ReadEvents(events);
        var passing = checkRows.Count(row => row.Status == "pass");

        return new ControlSnapshot(
            Available: true,
            Link: "live",
            Note: null,
            License: license,
            Monitoring: "dark",
            MonitoringNote: MonitoringDark,
            HeartbeatsActive: Number(heartbeats, "active"),
            HeartbeatsFailing: Number(heartbeats, "failing"),
            Unresolved: unresolvedTotal,
            ChecksPassing: passing,
            ChecksFailing: checkRows.Length - passing,
            Endpoints: endpoints,
            Checks: checkRows,
            Events: eventRows,
            QueueLength: null,
            Activity: []);
    }

    public readonly record struct MonitoringReading(
        string State,
        string Note,
        int? QueueLength,
        MonitoredActivity[] Activity);

    public static MonitoringReading ReadMonitoring(JsonElement endpoints)
    {
        if (endpoints.ValueKind != JsonValueKind.Array)
        {
            return new("down", "Monitoring did not answer.", null, []);
        }

        var activity = new List<MonitoredActivity>();
        foreach (var endpoint in endpoints.EnumerateArray())
        {
            var name = Text(endpoint, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            activity.Add(new MonitoredActivity(
                name,
                endpoint.TryGetProperty("isStale", out var stale) && stale.ValueKind == JsonValueKind.True,
                Trace(endpoint, "QueueLength"),
                Trace(endpoint, "Throughput"),
                Trace(endpoint, "ProcessingTime"),
                Trace(endpoint, "CriticalTime"),
                Trace(endpoint, "Retries")));
        }

        var series = activity.OrderBy(row => row.Name, StringComparer.Ordinal).ToArray();
        if (series.Length == 0)
        {
            return new("live", "No endpoint metrics yet.", 0, series);
        }

        var total = series.Sum(row => (int)Math.Round(row.QueueLength.Latest));
        var note = string.Join(" · ", series.Select(row => $"{row.Name} {(int)Math.Round(row.QueueLength.Latest)}"));
        return new("live", note, total, series);
    }

    static MetricTrace Trace(JsonElement endpoint, string metricName)
    {
        if (!endpoint.TryGetProperty("metrics", out var metrics) || metrics.ValueKind != JsonValueKind.Object)
        {
            return new MetricTrace(0, 0, []);
        }

        foreach (var metric in metrics.EnumerateObject())
        {
            if (!metric.Name.Equals(metricName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var average = metric.Value.TryGetProperty("average", out var mean) && mean.TryGetDouble(out var value)
                ? value
                : 0;
            var points = new List<double>();
            if (metric.Value.TryGetProperty("points", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var point in array.EnumerateArray())
                {
                    if (point.TryGetDouble(out var sample))
                    {
                        points.Add(sample);
                    }
                }
            }

            if (points.Count > 600)
            {
                points.RemoveRange(0, points.Count - 600);
            }

            var latest = points.Count > 0 ? points[^1] : average;
            return new MetricTrace(latest, average, points.ToArray());
        }

        return new MetricTrace(0, 0, []);
    }

    public static ControlEndpoint[] ReadEndpoints(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(row =>
            {
                var beat = Object(row, "heartbeat_information");
                return new ControlEndpoint(
                    Text(row, "name") ?? "",
                    Text(row, "host_display_name") ?? "",
                    Text(beat, "reported_status") ?? "unknown",
                    Text(beat, "last_report_at"));
            })
            .Where(row => row.Name.Length > 0)
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static ControlCheck[] ReadChecks(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(row => new ControlCheck(
                Text(row, "custom_check_id") ?? "",
                Text(row, "category") ?? "",
                Text(row, "status") ?? "",
                Text(row, "failure_reason")))
            .OrderBy(row => row.Status == "fail" ? 0 : 1)
            .ThenBy(row => row.Category, StringComparer.Ordinal)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static ControlEvent[] ReadEvents(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Take(12)
            .Select(row => new ControlEvent(
                Text(row, "description") ?? "",
                Text(row, "severity") ?? "",
                Text(row, "raised_at") ?? "",
                Text(row, "category") ?? "",
                Text(row, "event_type") ?? ""))
            .ToArray();
    }

    public static FailureItem[] ReadFailures(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(row =>
            {
                var exception = Object(row, "exception");
                return new FailureItem(
                    Text(row, "id") ?? "",
                    Text(row, "message_type") ?? "",
                    Text(row, "status") ?? "",
                    Text(exception, "exception_type"),
                    Text(exception, "message"),
                    EndpointName(row, "receiving_endpoint"),
                    EndpointName(row, "sending_endpoint"),
                    Text(row, "time_of_failure"),
                    Number(row, "number_of_processing_attempts"));
            })
            .Where(row => row.Id.Length > 0)
            .ToArray();
    }

    public static FailureDetail? ReadDetail(JsonElement row, string? body)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement attempt = default;
        if (row.TryGetProperty("processing_attempts", out var attempts) && attempts.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in attempts.EnumerateArray())
            {
                attempt = item;
            }
        }

        var metadata = Object(attempt, "message_metadata");
        var failure = Object(attempt, "failure_details");
        var exception = Object(failure, "exception");
        var contentType = Text(metadata, "ContentType");

        return new FailureDetail(
            Id: Text(row, "unique_message_id") ?? TrimFailedMessageId(Text(row, "id")),
            Status: Text(row, "status") ?? "",
            MessageType: Text(metadata, "MessageType") ?? "",
            ExceptionType: Text(exception, "exception_type"),
            ExceptionMessage: Text(exception, "message"),
            StackTrace: Text(exception, "stack_trace"),
            FailedAt: Text(failure, "time_of_failure"),
            ReceivingEndpoint: Text(failure, "address_of_failing_endpoint"),
            Body: body,
            ContentType: contentType);
    }

    public static string? BodyPath(JsonElement detail)
    {
        if (!detail.TryGetProperty("processing_attempts", out var attempts) || attempts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? path = null;
        foreach (var attempt in attempts.EnumerateArray())
        {
            var metadata = Object(attempt, "message_metadata");
            path = Text(metadata, "BodyUrl") ?? path;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith("/", StringComparison.Ordinal))
        {
            return "/api" + path;
        }

        return path;
    }

    public static FailureGroup[] ReadGroups(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(row => new FailureGroup(
                Text(row, "id") ?? "",
                Text(row, "title") ?? "",
                Text(row, "type") ?? "",
                Number(row, "count"),
                Text(row, "operation_status") ?? "",
                Double(row, "operation_progress")))
            .Where(row => row.Id.Length > 0)
            .ToArray();
    }

    public static TrafficItem[] ReadTraffic(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(row => new TrafficItem(
                Text(row, "id") ?? "",
                Text(row, "message_type") ?? "",
                EndpointName(row, "sending_endpoint"),
                EndpointName(row, "receiving_endpoint"),
                Text(row, "time_sent"),
                Text(row, "processed_at"),
                Text(row, "critical_time"),
                Text(row, "processing_time")))
            .Where(row => row.Id.Length > 0)
            .ToArray();
    }

    public static int ReadTotal(HttpResponseMessage response, int fallback)
    {
        if (response.Headers.TryGetValues("Total-Count", out var values)
            && int.TryParse(values.FirstOrDefault(), out var total))
        {
            return total;
        }

        return fallback;
    }

    private static string TrimFailedMessageId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        var slash = id.LastIndexOf('/');
        return slash >= 0 ? id[(slash + 1)..] : id;
    }

    private static JsonElement Object(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string? Text(JsonElement row, string name)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? EndpointName(JsonElement row, string name)
    {
        var endpoint = Object(row, name);
        return Text(endpoint, "name");
    }

    private static int Number(JsonElement row, string name)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }

    private static double Double(JsonElement row, string name)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : 0;
    }
}
