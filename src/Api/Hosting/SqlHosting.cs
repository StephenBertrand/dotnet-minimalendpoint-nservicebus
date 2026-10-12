using Api.Features.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Hosting;

public static class SqlHosting
{
    public const string ConnectionName = "todos";

    /// <summary>
    /// Local Aspire SQL container injects a SQL-auth connection string.
    /// Published Azure SQL injects Entra auth; the Aspire EF integration uses
    /// DefaultAzureCredential (managed identity in Azure, az login locally).
    /// </summary>
    public static TBuilder AddTodosDatabase<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(ConnectionName)))
        {
            builder.AddSqlServerDbContext<TodosDbContext>(ConnectionName);
        }
        else
        {
            builder.Services.AddDbContext<TodosDbContext>(options =>
                options.UseInMemoryDatabase("todos-without-sql"));
        }

        return builder;
    }

    public static async Task ApplyTodosSchemaAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodosDbContext>();

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
