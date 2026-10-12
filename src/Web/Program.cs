using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHttpForwarderWithServiceDiscovery();

var app = builder.Build();

app.MapDefaultEndpoints();

// Browser talks only to this site. Aspire service discovery resolves "api".
app.MapForwarder("/api/{**catch-all}", "https+http://api", "/{**catch-all}");

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
