using Azure.Data.AppConfiguration;
using Endpoint.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Api.Features.Processing;

sealed class AppConfigurationProcessingSwitch(ConfigurationClient client) : IProcessingSwitch
{
    public bool CanUpdate => true;

    public async Task<ProcessingCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in QueueGate.Keys())
        {
            values[key] = await ReadValueAsync(key, cancellationToken);
        }

        return ProcessingCatalogReader.From(true, key => values.GetValueOrDefault(key) ?? "");
    }

    public async Task SetAsync(string endpoint, string? message, string mode, CancellationToken cancellationToken)
    {
        await client.SetConfigurationSettingAsync(
            new ConfigurationSetting(QueueGate.Key(endpoint, message), mode),
            cancellationToken: cancellationToken);
        await BumpSentinelAsync(cancellationToken);
    }

    async Task<string?> ReadValueAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await client.GetConfigurationSettingAsync(key, cancellationToken: cancellationToken);
            return setting.Value.Value;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    async Task BumpSentinelAsync(CancellationToken cancellationToken)
    {
        var key = AzureAppConfigurationHosting.SentinelKey;
        string? current = null;
        try
        {
            var setting = await client.GetConfigurationSettingAsync(key, cancellationToken: cancellationToken);
            current = setting.Value.Value;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            current = null;
        }

        await client.SetConfigurationSettingAsync(
            new ConfigurationSetting(key, Sentinel.Next(current)),
            cancellationToken: cancellationToken);
    }
}
