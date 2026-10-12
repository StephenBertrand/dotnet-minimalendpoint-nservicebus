using Api.Features.Todos;
using Api.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Tests;

/// <summary>
/// Hosts the API against a real SQL Server connection string (Testcontainers).
/// </summary>
public sealed class SqlApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SqlApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{SqlHosting.ConnectionName}", _connectionString);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlHosting.ConnectionName}"] = _connectionString
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlHosting.ConnectionName}"] = _connectionString
            });
        });

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodosDbContext>();
        if (db.Database.ProviderName is not "Microsoft.EntityFrameworkCore.SqlServer")
        {
            throw new InvalidOperationException(
                "Todos tests must run against SQL Server. The Testcontainers connection string was not applied before the host was built.");
        }

        return host;
    }
}
