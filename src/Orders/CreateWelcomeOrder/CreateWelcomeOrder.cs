using Enrollment;
using Messages;
using Microsoft.EntityFrameworkCore;

namespace Orders.CreateWelcomeOrder;

public sealed class CreateWelcomeOrder(OrdersDbContext db)
{
    public async Task ExecuteAsync(CreateWelcomeOrderCommand message, CancellationToken cancellationToken)
    {
        var exists = await db.Orders.AnyAsync(row => row.TrackingId == message.TrackingId, cancellationToken);
        if (exists)
        {
            return;
        }

        db.Orders.Add(new OrderRecord
        {
            OrderId = Guid.NewGuid(),
            TrackingId = message.TrackingId,
            Name = message.Name,
            Email = message.Email,
            Summary = $"Welcome order for {message.Program}",
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
