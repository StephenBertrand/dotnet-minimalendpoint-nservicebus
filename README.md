# Minimal endpoints, SQL, and NServiceBus endpoints

This sample starts from the SQL playground and adds an enrollment that is accepted online and finished offline.

The API validates the form, returns **202 Accepted** with a tracking id, and sends one `EnrollStudentCommand`. It does not write a tracking row first, and it does not use the transactional outbox. The `Enrollment` endpoint receives the command, inserts the row, and marks it completed. A completed enrollment publishes `EnrollmentCompletedEvent`. The `Orders` event handler only sends `CreateWelcomeOrderCommand`. That command handler writes the welcome order, and it is safe to retry. The browser polls until the enrollment row appears.

| Environment | App Configuration | SQL | Messaging |
| --- | --- | --- | --- |
| Local (`dotnet run --project src/AppHost`) | Emulator container (image `1.2.0`) | Azure SQL resource **run as a SQL Server container** | RabbitMQ container, management on host port `15672` |
| Publish to Azure | App Configuration store with **DefaultAzureCredential** | **Azure SQL with Microsoft Entra authentication** | Service Bus namespace with **DefaultAzureCredential** |

## What you get

| Path | Behavior |
| --- | --- |
| `web` `/` | Playground, including the enrollment form |
| `web` `/api/*` | YARP forwarder to `api` |
| `api` `GET /hello`, `GET /ping`, `/todos` | Same as the SQL sample |
| `api` `POST /enrollments` | FluentValidation. Failures are `400` `application/problem+json`. Success is `202` with `{ trackingId }` and a `Location` header. |
| `api` `GET /enrollments/{trackingId}` | `404` until the handler has written the row, then `200` with `inprogress`, `completed`, or `rejected` |
| `api` `GET /orders` | Welcome orders written by the `Orders` endpoint |
| `api` `GET` / `PUT /processing` | Queue gates for each endpoint and each handler: `running`, `pause`, or `error` |
| `endpoint` | Two NServiceBus endpoints in one process: `Enrollment` and `Orders` |

The form field `processing` is `immediate` or `delayed`. Delayed messages use `DelayDeliveryWith` of one minute so the page can show the slow path. The page polls every 2 seconds. Within 10 seconds of completion it says the transaction is done. After 10 seconds it tells a joke and keeps waiting. After 2 minutes it stops and offers a button that polls the same tracking id again.

## Messaging

- Transport uses the `messaging` connection. A string that starts with `amqp://` or `amqps://` is RabbitMQ. Anything else is Azure Service Bus. Enrollment rows stay in SQL on the `enrollments` connection.
- A local run starts RabbitMQ. Publish provisions a Service Bus namespace and uses `DefaultAzureCredential`. Both use quorum or topic routing that matches the broker, and both receive with `TransportTransactionMode.ReceiveOnly`.
- The API endpoint is send-only and named `Api`. `EnrollStudentCommand` is routed to `Enrollment`.
- Delayed delivery is held by the broker, so the send-only API can delay a message and then exit. RabbitMQ uses its native delay infrastructure. Service Bus uses `ScheduledEnqueueTimeUtc`.
- Message names are the convention. A type whose name ends in `Command` is a command. A type whose name ends in `Event` is an event. Neither implements a NServiceBus marker interface.
- `Enrollment` and `Orders` run in the same process as separate endpoints. Assembly scanning is off, and each handler is registered on its own endpoint.
- NServiceBus starts only when `ConnectionStrings:messaging` is present. Unit tests use the in-memory databases and a fake sender.
- Welcome orders stay in SQL on the `orders` connection. The order handler is idempotent on the enrollment tracking id.

The handler commits outside the transport transaction (`TransactionScopeOption.Suppress` and a new `DbContext`). The first commit inserts `inprogress`. The second updates that row to `completed`. A poll can see `404`, then `inprogress`, then `completed`. On the immediate path those two commits are back to back, so the page usually goes from `404` to `completed`. The one-minute delay is what makes the wait visible.

A second delivery of the same tracking id is safe. A completed row is left as it is.

## Queue gates

App Configuration stores one string per endpoint and one string per handler under `Demo:Processing:`. A missing value means `running`. The playground Processing tab writes these keys and bumps `Demo:Sentinel`.

| Mode | What the pipeline does |
| --- | --- |
| `running` | The handler runs. |
| `pause` | Commands only. The handler does not run. The command is sent back to this endpoint with a 30 second delay. It does not go to the error queue. Events ignore pause and the handler runs. |
| `error` | The pipeline throws `RoutedToErrorException`. That exception is unrecoverable, so NServiceBus skips retries and moves the message to the error queue. |

An endpoint mode other than `running` covers that endpoint, except pause, which holds only commands. A handler mode applies only while its endpoint is running. `Orders` can be paused because it handles `CreateWelcomeOrderCommand`. The welcome-order event cannot. Route to error still applies to both. Holding `EnrollStudentCommand` also holds the welcome order, because the event is published only after that command completes. Holding `CreateWelcomeOrderCommand` holds the SQL write after the event handler has already sent it. The event handler does no I/O, so a failure there does not share a transaction with the order write.

`HandleCurrentMessageLater` would retry immediately and spin. Aborting the pipeline without a send would drop the message.

The endpoint process has no HTTP requests, so a hosted service refreshes App Configuration every 5 seconds. The API still refreshes through middleware as well. The Processing page reads the store directly, so the selects do not flip back while that cache is stale.

## ServicePulse

The AppHost runs the Particular platform on the same broker as the endpoints. Locally that broker is RabbitMQ. Publish uses the Service Bus namespace. Enrollment rows stay in SQL. The host adds:

| Resource | Role |
| --- | --- |
| `particular-error` | ServiceControl error instance. Reads the `error` queue. Heartbeats go to its input queue, `Particular.ServiceControl`. |
| `particular-audit` | ServiceControl audit instance. Reads the `audit` queue. |
| `particular-monitoring` | ServiceControl monitoring instance. Reads `Particular.Monitoring` and serves queue length. |
| `particular-servicepulse` | Stock ServicePulse UI, kept so the two screens can be compared. Monitoring is wired in, so the monitoring page is configured. |
| Flight control | `/control.html` on the web resource. Same ServiceControl data, drawn as a control room. The API reads `particular-error`, `particular-audit`, and `particular-monitoring`. The Activity tab lists each endpoint as five plots (queue length, throughput, retries, processing time, critical time). The number is the average for the selected window. History windows match ServicePulse: 1m, 5m, 10m, 15m, 30m, and 1h. Traffic opens on all audited endpoints. |
| `particular-persistence` | RavenDB for ServiceControl. Enrollment rows stay in SQL. |

`Enrollment` and `Orders` each audit processed messages, send heartbeats, and send metrics every 10 seconds. The send-only `Api` sends heartbeats. Metrics are not supported on a send-only endpoint. Queue length on the flight-control board comes from `/monitored-endpoints`. A missing monitoring reference stays dark. A failed call shows down. The board does not invent a number.

`pause` does not fail the command, so held work does not appear under Failed Messages. While a command gate is paused, new audit records for that command stop, heartbeats show the endpoint is still up, and the scheduled command waits on the broker. Set the gate back to `running` and the next 30 second delivery is audited as a success. A paused enrollment does not publish `EnrollmentCompletedEvent` until the command runs. `error` is what shows up as a failed message, for a command or an event, with no retries. A real handler exception still retries, then shows up as a failed message.

If no Particular license file is found, Aspire injects the placeholder `Trial`. ServiceControl then uses its own trial behavior. Put a `license.xml` where the host looks (`LocalApplicationData/ParticularSoftware` or `CommonApplicationData/ParticularSoftware`), or set `PARTICULARSOFTWARE_LICENSE`, to use a real license.

## Run

```bash
dotnet run --project src/AppHost
```

Open the **web** resource in the Aspire dashboard. Flight control is `/control.html` on that site. Stock ServicePulse is still `particular-servicepulse`. Docker must be running. The first start pulls RabbitMQ, its SQL Server container, ServiceControl (error, audit, and monitoring), ServicePulse, and RavenDB. RabbitMQ management is published on host port `15672`. The AMQP port is assigned by Aspire. The RabbitMQ user is `enrollment`, because RabbitMQ refuses `guest` from another container and ServiceControl connects from its own container.

To hold or flush a command, open the playground **Processing** tab. Pause applies to `EnrollStudentCommand`, `CreateWelcomeOrderCommand`, and either endpoint. The welcome-order event row has no pause control. The endpoint notices a change within about 5 seconds. Set a paused command back to **Running** and it is handled on its next 30 second delivery. A message routed to error stays in ServicePulse until it is retried or archived there.

## Add a feature

1. Folder under `src/Api/Features/<Name>/`
2. Implement `IEndpoint`
3. `AddFeatureEndpoints` registers it

Enrollment messages, the entity, and `EnrollmentsDbContext` live in `src/Enrollment` so the API and the endpoint share them. The handler stays in `src/Endpoint`.

## Tests

```bash
dotnet test
```

Hello, Ping, and Enrollment HTTP tests use `WebApplicationFactory`. They do not start NServiceBus. Enrollment processor tests use the EF in-memory provider. Todos tests start a SQL Server Testcontainer. Docker must be running for those.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Enrollment --startup-project src/Enrollment --context EnrollmentsDbContext --output-dir Migrations
dotnet ef migrations add <Name> --project src/Enrollment --startup-project src/Enrollment --context OrdersDbContext --output-dir Orders/Migrations
```

Todos migrations stay in `src/Api`:

```bash
dotnet ef migrations add <Name> --project src/Api --startup-project src/Api
```

Startup calls `MigrateAsync` for each relational database. The NServiceBus installers create queue tables after that, when the connection string is present.
