using System.Net.Http.Headers;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Endpoint.Extensions;

internal static class AppConfigurationEmulatorSeeder
{
    private const string ApiVersion = "2023-11-01";
    private const string FeatureFlagContentType = "application/vnd.microsoft.appconfig.ff+json;charset=utf-8";
    private const string HelloNewWorldFlag = "HelloNewWorld";
    private const string SentinelKey = "Demo:Sentinel";

    public static async Task SeedIfMissingAsync(AzureAppConfigurationResource resource, CancellationToken cancellationToken)
    {
        if (!resource.IsEmulator)
        {
            return;
        }

        var endpoint = await ResolveEndpointAsync(resource, cancellationToken);
        if (endpoint is null)
        {
            return;
        }

        using var client = new HttpClient { BaseAddress = endpoint };

        await PutIfMissingAsync(
            client,
            $".appconfig.featureflag/{HelloNewWorldFlag}",
            FeatureFlagContentType,
            """{"id":"HelloNewWorld","description":"When enabled, GET /hello returns Hello New World.","enabled":false,"conditions":{"client_filters":[]}}""",
            cancellationToken);

        foreach (var key in QueueGate.Keys())
        {
            await PutIfMissingAsync(client, key, contentType: null, value: "running", cancellationToken);
        }

        await PutIfMissingAsync(
            client,
            SentinelKey,
            contentType: null,
            value: "1",
            cancellationToken);
    }

    private static async Task<Uri?> ResolveEndpointAsync(AzureAppConfigurationResource resource, CancellationToken cancellationToken)
    {
        if (resource is IResourceWithConnectionString connection)
        {
            var connectionString = await connection.GetConnectionStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var separator = part.IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    var key = part[..separator];
                    var value = part[(separator + 1)..];
                    if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)
                        && Uri.TryCreate(value, UriKind.Absolute, out var fromConnection))
                    {
                        return fromConnection;
                    }
                }
            }
        }

        if (resource is IResourceWithEndpoints endpoints)
        {
            foreach (var name in new[] { "http", "https" })
            {
                try
                {
                    var url = endpoints.GetEndpoint(name).Url;
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        return uri;
                    }
                }
                catch (DistributedApplicationException)
                {
                    // Endpoint not allocated under this name.
                }
            }
        }

        return null;
    }

    private static async Task PutIfMissingAsync(
        HttpClient client,
        string key,
        string? contentType,
        string value,
        CancellationToken cancellationToken)
    {
        var path = $"/kv/{Uri.EscapeDataString(key)}?api-version={ApiVersion}";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var get = await client.GetAsync(path, cancellationToken);
                if (get.IsSuccessStatusCode)
                {
                    return;
                }

                if (get.StatusCode is not System.Net.HttpStatusCode.NotFound
                    && get.StatusCode is not System.Net.HttpStatusCode.Unauthorized
                    && (int)get.StatusCode < 500
                    && attempt < 9)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                    continue;
                }

                var payload = contentType is null
                    ? System.Text.Json.JsonSerializer.Serialize(new { value })
                    : System.Text.Json.JsonSerializer.Serialize(new { value, content_type = contentType });

                using var content = new StringContent(
                    payload,
                    Encoding.UTF8,
                    "application/vnd.microsoft.appconfig.kv+json");

                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.microsoft.appconfig.kv+json");

                using var put = await client.PutAsync(path, content, cancellationToken);
                if (put.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) when (attempt < 9)
            {
                // Emulator can still be binding its port when ResourceReady fires.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
        }
    }
}
