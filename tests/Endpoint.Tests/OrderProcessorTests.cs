using Enrollment;
using EnrollmentEndpoint;
using Microsoft.EntityFrameworkCore;

namespace Endpoint.Tests;

public sealed class OrderProcessorTests
{
    [Fact]
    public async Task CreateWelcomeOrder_InsertsOneRowPerTrackingId()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var processor = new OrderProcessor(options);
        var message = new CreateWelcomeOrderCommand
        {
            TrackingId = Guid.NewGuid(),
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Program = "Morning cohort"
        };

        await processor.CreateWelcomeOrderAsync(message, CancellationToken.None);
        await processor.CreateWelcomeOrderAsync(message, CancellationToken.None);

        await using var db = new OrdersDbContext(options);
        var row = await db.Orders.SingleAsync();
        Assert.Equal(message.TrackingId, row.TrackingId);
        Assert.Equal("Welcome order for Morning cohort", row.Summary);
    }
}
