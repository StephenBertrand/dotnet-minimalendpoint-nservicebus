using System.Transactions;
using Enrollment;
using Microsoft.EntityFrameworkCore;

namespace EnrollmentEndpoint;

public sealed class OrderProcessor(DbContextOptions<OrdersDbContext> options)
{
    public async Task CreateWelcomeOrderAsync(CreateWelcomeOrderCommand message, CancellationToken cancellationToken)
    {
        using var suppress = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var db = new OrdersDbContext(options);
        var exists = await db.Orders.AnyAsync(row => row.TrackingId == message.TrackingId, cancellationToken);
        if (exists)
        {
            suppress.Complete();
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
        suppress.Complete();
    }
}
