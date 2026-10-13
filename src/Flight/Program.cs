var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHttpForwarderWithServiceDiscovery();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/playground", (IConfiguration configuration) => SiteLink.Redirect(configuration, "web"));

// Browser talks only to this site. Aspire service discovery resolves "api".
app.MapForwarder("/api/{**catch-all}", "https+http://api", "/{**catch-all}");

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = response => response.Context.Response.Headers.CacheControl = "no-cache"
});

app.Run();

static class SiteLink
{
    public static IResult Redirect(IConfiguration configuration, string name)
    {
        var url = configuration[$"services:{name}:https:0"]
            ?? configuration[$"services:{name}:http:0"];
        return url is null
            ? Results.NotFound()
            : Results.Redirect(url);
    }
}
