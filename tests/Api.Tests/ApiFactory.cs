using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}

public sealed class HelloApiFactory : WebApplicationFactory<Program>
{
    private readonly bool _helloNewWorld;

    public HelloApiFactory(bool helloNewWorld)
    {
        _helloNewWorld = helloNewWorld;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:HelloNewWorld"] = _helloNewWorld ? "true" : "false"
            });
        });
    }
}
