using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endpoint.Extensions;
using Microsoft.Extensions.Hosting;

namespace Api.Features.Processing;

sealed class AnonymousProcessingSwitch(Uri endpoint) : IProcessingSwitch
{
    const string ApiVersion = "2023-11-01";
    const string KvContentType = "application/vnd.microsoft.appconfig.kv+json";

    readonly HttpClient client = new() { BaseAddress = endpoint };

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

    public async Task SetAsync(string endpointName, string? message, string mode, CancellationToken cancellationToken)
    {
        await PutAsync(QueueGate.Key(endpointName, message), mode, cancellationToken);
        var sentinel = await ReadValueAsync(AzureAppConfigurationHosting.SentinelKey, cancellationToken);
        await PutAsync(AzureAppConfigurationHosting.SentinelKey, Sentinel.Next(sentinel), cancellationToken);
    }

    async Task<string?> ReadValueAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(KeyPath(key), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var document = await JsonNode.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return document?["value"]?.GetValue<string>();
    }

    async Task PutAsync(string key, string value, CancellationToken cancellationToken)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { value }), Encoding.UTF8, KvContentType);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(KvContentType);
        using var response = await client.PutAsync(KeyPath(key), content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    static string KeyPath(string key) =>
        $"/kv/{Uri.EscapeDataString(key)}?api-version={ApiVersion}";
}
