using Enrollment;
using Messages;
using Microsoft.EntityFrameworkCore;
using Orders.CreateWelcomeOrder;

namespace Endpoint.Tests;

public sealed class CreateWelcomeOrderTests
{
    [Fact]
    public async Task Execute_InsertsOneRowPerTrackingId()
    {
        await using var db = NewDatabase();
        var create = new CreateWelcomeOrder(db);
        var message = new CreateWelcomeOrderCommand
        {
            TrackingId = Guid.NewGuid(),
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Program = "Morning cohort"
        };

        await create.ExecuteAsync(message, CancellationToken.None);
        await create.ExecuteAsync(message, CancellationToken.None);

        var row = await db.Orders.SingleAsync();
        Assert.Equal(message.TrackingId, row.TrackingId);
        Assert.Equal("Welcome order for Morning cohort", row.Summary);
    }

    static OrdersDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrdersDbContext(options);
    }
}
