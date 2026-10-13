using Azure.Data.AppConfiguration;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Features.Processing;

public interface IProcessingSwitch
{
    bool CanUpdate { get; }

    Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken);

    Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken);
}

public static class ProcessingSwitchHosting
{
    public static TBuilder AddProcessingSwitch<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (TryCreateAnonymousStore(builder.Configuration, out var endpoint))
        {
            builder.Services.AddSingleton<IProcessingSwitch>(_ => new AnonymousProcessingSwitch(endpoint));
            return builder;
        }

        var client = CreateClient(builder.Configuration);
        if (client is null)
        {
            builder.Services.AddSingleton<IProcessingSwitch, UnavailableProcessingSwitch>();
            return builder;
        }

        builder.Services.AddSingleton(client);
        builder.Services.AddSingleton<IProcessingSwitch, AppConfigurationProcessingSwitch>();
        return builder;
    }

    private static bool TryCreateAnonymousStore(IConfiguration configuration, out Uri endpoint)
    {
        endpoint = null!;
        var connectionString = configuration.GetConnectionString(AzureAppConfigurationHosting.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString)
            || !connectionString.Contains("Anonymous=true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parsed = ConnectionStringValue.Endpoint(connectionString);
        if (parsed is null)
        {
            return false;
        }

        endpoint = parsed;
        return true;
    }

    private static ConfigurationClient? CreateClient(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AzureAppConfigurationHosting.ConnectionName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (connectionString.Contains("Secret=", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Id=", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigurationClient(connectionString);
            }

            if (Uri.TryCreate(connectionString, UriKind.Absolute, out var fromConnection))
            {
                return new ConfigurationClient(fromConnection, new DefaultAzureCredential());
            }
        }

        var endpoint = configuration["Aspire:Microsoft:Extensions:Configuration:AzureAppConfiguration:Endpoint"]
            ?? configuration[$"{AzureAppConfigurationHosting.ConnectionName}:endpoint"];
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var fromEndpoint))
        {
            return new ConfigurationClient(fromEndpoint, new DefaultAzureCredential());
        }

        return null;
    }
}

static class ConnectionStringValue
{
    public static Uri? Endpoint(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (part[..separator].Equals("Endpoint", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(part[(separator + 1)..], UriKind.Absolute, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}

sealed class UnavailableProcessingSwitch : IProcessingSwitch
{
    public bool CanUpdate => false;

    public Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ProcessingCatalogReader.Running(canUpdate: false));

    public Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App Configuration is not configured.");
}
