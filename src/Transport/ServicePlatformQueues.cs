namespace Transport;

/// <summary>
/// Queue names that match the Particular Aspire platform defaults.
/// Heartbeats go to the ServiceControl error instance input queue, not the failed-message queue.
/// </summary>
public static class ServicePlatformQueues
{
    public const string Error = "error";
    public const string Audit = "audit";
    public const string Heartbeats = "Particular.ServiceControl";
    public const string Monitoring = "Particular.Monitoring";

    public static readonly TimeSpan MetricsInterval = TimeSpan.FromSeconds(10);
}
