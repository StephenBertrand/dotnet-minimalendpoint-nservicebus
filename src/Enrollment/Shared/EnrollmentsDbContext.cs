using Microsoft.EntityFrameworkCore;

namespace Enrollment;

public sealed class EnrollmentsDbContext(DbContextOptions<EnrollmentsDbContext> options) : DbContext(options)
{
    public DbSet<EnrollmentRecord> Enrollments => Set<EnrollmentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var enrollment = modelBuilder.Entity<EnrollmentRecord>();
        enrollment.ToTable("Enrollments");
        enrollment.HasKey(row => row.TrackingId);
        enrollment.Property(row => row.Name).HasMaxLength(100).IsRequired();
        enrollment.Property(row => row.Email).HasMaxLength(200).IsRequired();
        enrollment.Property(row => row.Program).HasMaxLength(100).IsRequired();
        enrollment.Property(row => row.Status).HasMaxLength(20).IsRequired();
        enrollment.Property(row => row.Message).HasMaxLength(500);
        enrollment.Property(row => row.CreatedUtc).IsRequired();
        enrollment.HasIndex(row => row.Email)
            .IsUnique()
            .HasFilter("[Status] = 'completed'")
            .HasDatabaseName("IX_Enrollments_CompletedEmail");
    }
}
