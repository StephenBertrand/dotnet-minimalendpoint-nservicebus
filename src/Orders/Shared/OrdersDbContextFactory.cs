using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Enrollment;

public sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=orders;User Id=sa;Password=design-time;TrustServerCertificate=True")
            .Options;

        return new OrdersDbContext(options);
    }
}
