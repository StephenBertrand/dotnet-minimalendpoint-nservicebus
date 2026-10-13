using Enrollment;
using EnrollmentEndpoint;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddDemoAzureAppConfiguration();
builder.AddEnrollmentStore();
builder.AddOrderStore();
builder.AddEnrollmentProcessingEndpoints();

var app = builder.Build();

await app.ApplyEnrollmentSchemaAsync();
await app.ApplyOrderSchemaAsync();

app.Run();
