using Microsoft.EntityFrameworkCore;

namespace Enrollment;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<OrderRecord>();
        order.ToTable("Orders");
        order.HasKey(row => row.OrderId);
        order.Property(row => row.Name).HasMaxLength(100).IsRequired();
        order.Property(row => row.Email).HasMaxLength(200).IsRequired();
        order.Property(row => row.Summary).HasMaxLength(200).IsRequired();
        order.Property(row => row.CreatedUtc).IsRequired();
        order.HasIndex(row => row.TrackingId).IsUnique();
    }
}
