var builder = DistributedApplication.CreateBuilder(args);

var appConfiguration = builder.AddAzureAppConfiguration("appconfiguration");

if (builder.ExecutionContext.IsRunMode)
{
    appConfiguration.RunAsEmulator(emulator =>
    {
        // Aspire 13.5.3 defaults to 1.0.2, which has no linux/arm64 manifest.
        emulator.WithImageTag("1.2.0");
        emulator.WithLifetime(ContainerLifetime.Persistent);
        emulator.WithDataVolume();
    });

    appConfiguration.OnResourceReady(async (resource, _, cancellationToken) =>
        await AppConfigurationEmulatorSeeder.SeedIfMissingAsync(resource, cancellationToken));
}

// Azure SQL with Entra-only auth when published. Locally this is a SQL Server container
// (SQL auth, Aspire-generated password). The API still uses one connection name: "todos".
var sql = builder.AddAzureSqlServer("sql");

if (builder.ExecutionContext.IsRunMode)
{
    sql.RunAsContainer(container =>
    {
        container.WithLifetime(ContainerLifetime.Persistent);
        container.WithDataVolume();
    });
}

var todos = sql.AddDatabase("todos");
var enrollments = sql.AddDatabase("enrollments");

// Local run uses the Service Bus emulator (image 2.0.0, which supports the administration client).
// Publish provisions a real namespace. Ports 5672 and 5300 stay fixed so NServiceBus can reach
// the emulator's AMQP endpoint and its administration endpoint.
var messaging = builder.AddAzureServiceBus("messaging");

if (builder.ExecutionContext.IsRunMode)
{
    messaging.RunAsEmulator(emulator =>
    {
        emulator.WithHostPort(5672);
        emulator.WithEndpoint("emulatorhealth", endpoint => endpoint.Port = 5300);
    });
}

// Declared up front so the emulator config and the published namespace both contain them.
// ServiceControl setup also creates its own internal queues when the administration client is available.
messaging.AddServiceBusQueue("sb-enrollment", "Enrollment");
messaging.AddServiceBusQueue("sb-error", "error");
messaging.AddServiceBusQueue("sb-audit", "audit");
messaging.AddServiceBusQueue("sb-servicecontrol", "Particular.ServiceControl");
messaging.AddServiceBusQueue("sb-monitoring", "Particular.Monitoring");

// ServiceControl stores its own data in RavenDB. Enrollment rows stay in SQL.
// The emulator's administration client listens on port 5300. ServiceControl's topic
// setup uses the data-plane connection string, so it never reaches that port. These
// integration events are not what ServicePulse reads, so leave them unpublished.
var platform = builder.AddParticularPlatform("particular")
    .WithTransportAzureServiceBus(messaging);
var persistence = platform.AddPersistenceRavenDb("particular-persistence");
platform.AddServiceControlErrorInstance("particular-error", persistence)
    .WithEnvironment("SERVICECONTROL_DISABLEEXTERNALINTEGRATIONSPUBLISHING", "True");
platform.AddDefaultComponents();

var endpoint = builder.AddProject<Projects.Endpoint>("endpoint")
    .WithReference(appConfiguration)
    .WaitFor(appConfiguration)
    .WithReference(enrollments)
    .WaitFor(enrollments)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithParticularPlatform(platform)
    .WaitFor(platform);

var api = builder.AddProject<Projects.Api>("api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(appConfiguration)
    .WaitFor(appConfiguration)
    .WithReference(todos)
    .WaitFor(todos)
    .WithReference(enrollments)
    .WaitFor(enrollments)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithParticularPlatform(platform)
    .WaitFor(platform)
    .WaitFor(endpoint);

builder.AddProject<Projects.Web>("web")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
