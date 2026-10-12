using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Enrollment;

public sealed class EnrollmentsDbContextFactory : IDesignTimeDbContextFactory<EnrollmentsDbContext>
{
    public EnrollmentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EnrollmentsDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=enrollments;User Id=sa;Password=design-time;TrustServerCertificate=True")
            .Options;

        return new EnrollmentsDbContext(options);
    }
}
