namespace Api.Features.Control;

public sealed record ControlSnapshot(
    bool Available,
    string Link,
    string? Note,
    string? License,
    string Monitoring,
    string MonitoringNote,
    int HeartbeatsActive,
    int HeartbeatsFailing,
    int Unresolved,
    int ChecksPassing,
    int ChecksFailing,
    ControlEndpoint[] Endpoints,
    ControlCheck[] Checks,
    ControlEvent[] Events,
    int? QueueLength,
    MonitoredActivity[] Activity);

public sealed record MonitoredActivity(
    string Name,
    bool Stale,
    MetricTrace QueueLength,
    MetricTrace Throughput,
    MetricTrace ProcessingTime,
    MetricTrace CriticalTime,
    MetricTrace Retries);

public sealed record MetricTrace(double Latest, double Average, double[] Points);

public sealed record ActivityPage(int History, string Monitoring, string Note, MonitoredActivity[] Endpoints);

public sealed record ControlEndpoint(string Name, string Host, string Status, string? LastReportAt);

public sealed record ControlCheck(string Name, string Category, string Status, string? Reason);

public sealed record ControlEvent(
    string Description,
    string Severity,
    string RaisedAt,
    string Category,
    string EventType);

public sealed record FailureList(int Total, FailureItem[] Messages);

public sealed record FailureItem(
    string Id,
    string MessageType,
    string Status,
    string? ExceptionType,
    string? ExceptionMessage,
    string? ReceivingEndpoint,
    string? SendingEndpoint,
    string? FailedAt,
    int Attempts);

public sealed record FailureDetail(
    string Id,
    string Status,
    string MessageType,
    string? ExceptionType,
    string? ExceptionMessage,
    string? StackTrace,
    string? FailedAt,
    string? ReceivingEndpoint,
    string? Body,
    string? ContentType);

public sealed record GroupList(string Classifier, FailureGroup[] Groups);

public sealed record FailureGroup(
    string Id,
    string Title,
    string Type,
    int Count,
    string OperationStatus,
    double Progress);

public sealed record TrafficList(bool Available, string Endpoint, string? Note, TrafficItem[] Messages);

public sealed record TrafficItem(
    string Id,
    string MessageType,
    string? From,
    string? To,
    string? SentAt,
    string? ProcessedAt,
    string? CriticalTime,
    string? ProcessingTime);

public sealed record GroupAction(string? Id);

public sealed record ControlActionResult(bool Accepted);
