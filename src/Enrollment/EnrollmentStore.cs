using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Enrollment;

public static class EnrollmentStore
{
    public const string ConnectionName = "enrollments";

    public static TBuilder AddEnrollmentStore<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(ConnectionName)))
        {
            builder.AddSqlServerDbContext<EnrollmentsDbContext>(ConnectionName);
        }
        else
        {
            builder.Services.AddDbContext<EnrollmentsDbContext>(options =>
                options.UseInMemoryDatabase("enrollments"));
        }

        return builder;
    }

    public static async Task ApplyEnrollmentSchemaAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnrollmentsDbContext>();

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
