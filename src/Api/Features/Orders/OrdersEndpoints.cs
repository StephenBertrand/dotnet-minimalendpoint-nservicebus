using Api.Hosting;
using Enrollment;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Orders;

public sealed class OrdersEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/orders", ListAsync).WithTags("Orders").WithName("ListOrders");
    }

    private static async Task<IResult> ListAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.Orders.AsNoTracking()
            .OrderByDescending(row => row.CreatedUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Results.Ok(rows.Select(row => new OrderResponse(
            row.OrderId,
            row.TrackingId,
            row.Name,
            row.Email,
            row.Summary,
            row.CreatedUtc)).ToArray());
    }
}

public sealed record OrderResponse(
    Guid OrderId,
    Guid TrackingId,
    string Name,
    string Email,
    string Summary,
    DateTimeOffset CreatedUtc);
