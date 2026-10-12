using Enrollment;
using EnrollmentEndpoint;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddDemoAzureAppConfiguration();
builder.AddEnrollmentStore();
builder.AddEnrollmentProcessingEndpoint();

var app = builder.Build();

await app.ApplyEnrollmentSchemaAsync();

app.Run();
