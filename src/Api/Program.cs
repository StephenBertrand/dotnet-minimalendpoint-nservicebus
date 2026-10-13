using Api.Features.Control;
using Api.Features.Enrollments;
using Api.Features.Processing;
using Api.Hosting;
using Endpoint.Extensions;
using FluentValidation;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddDemoAzureAppConfiguration();
builder.AddProcessingSwitch();
builder.AddTodosDatabase();
builder.AddFeatureStorage(typeof(Program).Assembly);
builder.AddApiMessaging();
builder.AddControlRoom();
builder.Services.AddValidatorsFromAssemblyContaining<EnrollRequestValidator>();
builder.Services.AddOpenApi();
builder.Services.AddFeatureEndpoints(typeof(Program).Assembly);

var app = builder.Build();

await app.ApplyTodosSchemaAsync();
await app.PrepareFeatureStorageAsync(typeof(Program).Assembly);

app.MapDefaultEndpoints();

if (AzureAppConfigurationHosting.HasAppConfigurationConnection(app.Configuration)
    && app.Services.GetService<IConfigurationRefresherProvider>() is not null)
{
    app.UseAzureAppConfiguration();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapFeatures();

app.Run();

public partial class Program;
