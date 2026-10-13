using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Control;

namespace Api.Tests;

public class ControlEndpointsTests
{
    [Fact]
    public async Task Get_WhenServiceControlIsNotReferenced_ReportsTheLinkDark()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<ControlSnapshot>("/control");

        Assert.NotNull(response);
        Assert.False(response.Available);
        Assert.Equal("unconfigured", response.Link);
        Assert.Equal("dark", response.Monitoring);
        Assert.Null(response.QueueLength);
        Assert.Empty(response.Endpoints);
    }

    [Fact]
    public async Task Retry_WhenServiceControlIsNotReferenced_Returns503()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/control/failures/{Guid.NewGuid():D}/retry", new { });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public void Reader_ProjectsTheLiveServiceControlShapes()
    {
        using var endpoints = JsonDocument.Parse("""
            [{
              "name": "Orders",
              "host_display_name": "Mac",
              "heartbeat_information": { "last_report_at": "2026-10-12T23:05:42Z", "reported_status": "beating" }
            }]
            """);
        using var checks = JsonDocument.Parse("""
            [{
              "custom_check_id": "Dead Letter Queue",
              "category": "Transport",
              "status": "fail",
              "failure_reason": "admin client missed port 5300"
            }]
            """);
        using var failures = JsonDocument.Parse("""
            [{
              "id": "fa61288a-e8a1-ca96-f842-619e53a5d6a5",
              "message_type": "Messages.EnrollStudentCommand",
              "status": "archived",
              "number_of_processing_attempts": 1,
              "time_of_failure": "2026-10-12T22:57:54Z",
              "exception": { "exception_type": "EnrollmentEndpoint.RoutedToErrorException", "message": "routed" },
              "sending_endpoint": { "name": "Api" },
              "receiving_endpoint": { "name": "Enrollment" }
            }]
            """);
        using var detail = JsonDocument.Parse("""
            {
              "id": "FailedMessages/fa61288a-e8a1-ca96-f842-619e53a5d6a5",
              "unique_message_id": "fa61288a-e8a1-ca96-f842-619e53a5d6a5",
              "status": "archived",
              "processing_attempts": [{
                "message_metadata": { "MessageType": "Messages.EnrollStudentCommand", "ContentType": "application/json", "BodyUrl": "/messages/fa61288a-e8a1-ca96-f842-619e53a5d6a5/body" },
                "failure_details": {
                  "address_of_failing_endpoint": "Enrollment",
                  "time_of_failure": "2026-10-12T22:57:54Z",
                  "exception": { "exception_type": "EnrollmentEndpoint.RoutedToErrorException", "message": "routed", "stack_trace": "at QueueGate" }
                }
              }]
            }
            """);

        var endpoint = Assert.Single(ControlReader.ReadEndpoints(endpoints.RootElement));
        Assert.Equal("Orders", endpoint.Name);
        Assert.Equal("beating", endpoint.Status);

        var check = Assert.Single(ControlReader.ReadChecks(checks.RootElement));
        Assert.Equal("fail", check.Status);

        var failure = Assert.Single(ControlReader.ReadFailures(failures.RootElement));
        Assert.Equal("Enrollment", failure.ReceivingEndpoint);
        Assert.Equal(1, failure.Attempts);

        Assert.Equal("/api/messages/fa61288a-e8a1-ca96-f842-619e53a5d6a5/body", ControlReader.BodyPath(detail.RootElement));
        var read = ControlReader.ReadDetail(detail.RootElement, """{"email":"a@b.c"}""");
        Assert.NotNull(read);
        Assert.Equal("fa61288a-e8a1-ca96-f842-619e53a5d6a5", read.Id);
        Assert.Contains("QueueGate", read.StackTrace);

        using var monitored = JsonDocument.Parse("""
            [
              { "name": "Enrollment", "metrics": { "QueueLength": { "average": 0, "points": [0, 1] } } },
              { "name": "Orders", "metrics": { "QueueLength": { "average": 4, "points": [4] } } }
            ]
            """);
        var reading = ControlReader.ReadMonitoring(monitored.RootElement);
        Assert.Equal("live", reading.State);
        Assert.Equal(5, reading.QueueLength);
        Assert.Equal("Enrollment 1 · Orders 4", reading.Note);
        Assert.Equal(["Enrollment", "Orders"], reading.Activity.Select(row => row.Name).ToArray());
        Assert.Equal([0d, 1d], reading.Activity[0].QueueLength.Points);
        Assert.Equal(1, reading.Activity[0].QueueLength.Latest);
        Assert.Equal(4, reading.Activity[1].QueueLength.Latest);
        Assert.Empty(reading.Activity[0].Throughput.Points);
    }

    [Fact]
    public void Reader_KeepsSamplesBeyondOneMinute()
    {
        var points = string.Join(',', Enumerable.Range(0, 61));
        using var monitored = JsonDocument.Parse(
            $$"""[{ "name": "Enrollment", "metrics": { "QueueLength": { "average": 1, "points": [{{points}}] } } }]""");

        var reading = ControlReader.ReadMonitoring(monitored.RootElement);

        Assert.Equal(61, Assert.Single(reading.Activity).QueueLength.Points.Length);
    }

    private sealed record ControlSnapshot(
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
        int? QueueLength);

    private sealed record ControlEndpoint(string Name, string Host, string Status, string? LastReportAt);

    private sealed record ControlCheck(string Name, string Category, string Status, string? Reason);

    private sealed record ControlEvent(string Description, string Severity, string RaisedAt, string Category, string EventType);
}
