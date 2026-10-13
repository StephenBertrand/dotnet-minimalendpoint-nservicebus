using Endpoint.Extensions;
using EnrollmentEndpoint;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddDemoAzureAppConfiguration();
builder.AddWorkerEndpoints();

var app = builder.Build();

await app.PrepareFeatureStorageAsync(typeof(WorkerEndpoints).Assembly);

app.Run();
