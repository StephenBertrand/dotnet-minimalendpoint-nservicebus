using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Api.Tests;

/// <summary>
/// One SQL Server container for the test assembly. Individual tests get isolated
/// databases so CRUD is real (migrations, constraints) without sharing rows.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same family Aspire uses locally. Pin a CU tag if you need bit-for-bit CI.
    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public string CreateIsolatedConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"todos_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql-server";
}
