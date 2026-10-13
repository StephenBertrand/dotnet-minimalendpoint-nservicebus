using Enrollment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orders;

public static class OrderStore
{
    public const string ConnectionName = "orders";

    public static TBuilder AddOrderStore<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(ConnectionName)))
        {
            builder.AddSqlServerDbContext<OrdersDbContext>(ConnectionName);
        }
        else
        {
            builder.Services.AddDbContext<OrdersDbContext>(options =>
                options.UseInMemoryDatabase("orders"));
        }

        return builder;
    }

    public static async Task ApplyOrderSchemaAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
