using Enrollment;
using Microsoft.Extensions.Configuration;

namespace Endpoint.Tests;

public sealed class QueueGateTests
{
    [Fact]
    public void Effective_UsesTheEndpointModeWhenItIsNotRunning()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Processing:Enrollment"] = "pause",
                ["Demo:Processing:Enrollment:EnrollStudentCommand"] = "error"
            })
            .Build();

        var mode = QueueGate.Effective(configuration, "Enrollment", "EnrollStudentCommand");

        Assert.Equal(QueueGate.Pause, mode);
    }

    [Fact]
    public void Effective_UsesTheHandlerModeWhenTheEndpointIsRunning()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Processing:Orders"] = "running",
                ["Demo:Processing:Orders:EnrollmentCompletedEvent"] = "error"
            })
            .Build();

        var mode = QueueGate.Effective(configuration, "Orders", "EnrollmentCompletedEvent");

        Assert.Equal(QueueGate.Error, mode);
    }

    [Fact]
    public void Pause_HoldsCommandsAndLetsEventsContinue()
    {
        Assert.True(QueueGate.Pauses(QueueGate.Pause, "EnrollStudentCommand"));
        Assert.True(QueueGate.Pauses(QueueGate.Pause, "CreateWelcomeOrderCommand"));
        Assert.False(QueueGate.Pauses(QueueGate.Pause, "EnrollmentCompletedEvent"));
        Assert.False(QueueGate.Pauses(QueueGate.Error, "EnrollStudentCommand"));
        Assert.True(QueueGate.CanPause(QueueGate.EnrollmentEndpoint, "EnrollStudentCommand"));
        Assert.True(QueueGate.CanPause(QueueGate.OrdersEndpoint, "CreateWelcomeOrderCommand"));
        Assert.False(QueueGate.CanPause(QueueGate.OrdersEndpoint, "EnrollmentCompletedEvent"));
        Assert.True(QueueGate.CanPause(QueueGate.OrdersEndpoint, null));
    }
}
