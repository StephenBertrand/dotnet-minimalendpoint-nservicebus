using Api.Features.Enrollments;
using Api.Hosting;
using Enrollment;
using FluentValidation;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddDemoAzureAppConfiguration();
builder.AddTodosDatabase();
builder.AddEnrollmentStore();
builder.AddEnrollmentMessaging();
builder.Services.AddValidatorsFromAssemblyContaining<EnrollRequestValidator>();
builder.Services.AddOpenApi();
builder.Services.AddFeatureEndpoints(typeof(Program).Assembly);

var app = builder.Build();

await app.ApplyTodosSchemaAsync();
await app.ApplyEnrollmentSchemaAsync();

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
