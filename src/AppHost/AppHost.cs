using Particular.Aspire.Hosting.ServicePlatform.Transport;

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
var orders = sql.AddDatabase("orders");

// Local run uses RabbitMQ. Publish provisions a real Service Bus namespace.
// The username is not guest: RabbitMQ refuses the guest user from another container,
// and ServiceControl connects from its own container. Management stays on 15672 so the
// transport can check quorum delivery limits from the host.
IResourceBuilder<IResourceWithConnectionString> messaging;
var platform = builder.AddParticularPlatform("particular");
if (builder.ExecutionContext.IsRunMode)
{
    var rabbitUser = builder.AddParameter("rabbit-username", "enrollment");
    var rabbit = builder.AddRabbitMQ("messaging", rabbitUser)
        .WithManagementPlugin(15672);
    messaging = rabbit;
    platform = platform.WithTransportRabbitMQ(RabbitMqRouting.QuorumConventionalRouting, rabbit);
}
else
{
    var serviceBus = builder.AddAzureServiceBus("messaging");
    serviceBus.AddServiceBusQueue("sb-enrollment", "Enrollment");
    serviceBus.AddServiceBusQueue("sb-orders", "Orders");
    serviceBus.AddServiceBusQueue("sb-error", "error");
    serviceBus.AddServiceBusQueue("sb-audit", "audit");
    serviceBus.AddServiceBusQueue("sb-servicecontrol", "Particular.ServiceControl");
    serviceBus.AddServiceBusQueue("sb-monitoring", "Particular.Monitoring");
    messaging = serviceBus;
    platform = platform.WithTransportAzureServiceBus(serviceBus);
}

// ServiceControl stores its own data in RavenDB. Enrollment rows stay in SQL.
var persistence = platform.AddPersistenceRavenDb("particular-persistence");
var errorInstance = platform.AddServiceControlErrorInstance("particular-error", persistence);
var auditInstance = platform.AddServiceControlAuditInstance("particular-audit", errorInstance, persistence);
var monitoringInstance = platform.AddServiceControlMonitoringInstance("particular-monitoring");
platform.AddServicePulse("particular-servicepulse", errorInstance, monitoringInstance);

var endpoint = builder.AddProject<Projects.Endpoint>("endpoint")
    .WithReference(appConfiguration)
    .WaitFor(appConfiguration)
    .WithReference(enrollments)
    .WaitFor(enrollments)
    .WithReference(orders)
    .WaitFor(orders)
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
    .WithReference(orders)
    .WaitFor(orders)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithReference(errorInstance.GetEndpoint("http"))
    .WithReference(auditInstance.GetEndpoint("http"))
    .WithReference(monitoringInstance.GetEndpoint("http"))
    .WithParticularPlatform(platform)
    .WaitFor(platform)
    .WaitFor(endpoint);

builder.AddProject<Projects.Web>("web")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
