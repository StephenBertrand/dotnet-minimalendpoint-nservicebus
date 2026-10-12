# Minimal endpoints, SQL, and one NServiceBus endpoint

This sample starts from the SQL playground and adds an enrollment that is accepted online and finished offline.

The API validates the form, returns **202 Accepted** with a tracking id, and sends one `EnrollStudent` command. It does not write a tracking row first, and it does not use the transactional outbox. A single console endpoint, `Enrollment`, receives the command, inserts the row, and marks it completed. The browser polls until that row appears.

| Environment | App Configuration | SQL | Service Bus |
| --- | --- | --- | --- |
| Local (`dotnet run --project src/AppHost`) | Emulator container (image `1.2.0`) | Azure SQL resource **run as a SQL Server container** | Emulator container (image `2.0.0`) |
| Publish to Azure | App Configuration store with **DefaultAzureCredential** | **Azure SQL with Microsoft Entra authentication** | Namespace with **DefaultAzureCredential** |

## What you get

| Path | Behavior |
| --- | --- |
| `web` `/` | Playground, including the enrollment form |
| `web` `/api/*` | YARP forwarder to `api` |
| `api` `GET /hello`, `GET /ping`, `/todos` | Same as the SQL sample |
| `api` `POST /enrollments` | FluentValidation. Failures are `400` `application/problem+json`. Success is `202` with `{ trackingId }` and a `Location` header. |
| `api` `GET /enrollments/{trackingId}` | `404` until the handler has written the row, then `200` with `inprogress`, `completed`, or `rejected` |
| `endpoint` | One NServiceBus endpoint named `Enrollment` |

The form field `processing` is `immediate` or `delayed`. Delayed messages use `DelayDeliveryWith` of one minute so the page can show the slow path. The page polls every 2 seconds. Within 10 seconds of completion it says the transaction is done. After 10 seconds it tells a joke and keeps waiting. After 2 minutes it stops and offers a button that polls the same tracking id again.

## Messaging

- Transport is Azure Service Bus on the `messaging` connection. Enrollment rows stay in SQL on the `enrollments` connection.
- Locally the AppHost runs the Service Bus emulator. Publish uses a real namespace and `DefaultAzureCredential`.
- The API endpoint is send-only and named `Api`. `EnrollStudent` is routed to `Enrollment`.
- Delayed delivery is native to Service Bus (`ScheduledEnqueueTimeUtc`), so the send-only API can delay a message and then exit. The broker holds it.
- Commands are mapped with an unobtrusive convention. `EnrollStudent` does not implement `ICommand`.
- NServiceBus starts only when `ConnectionStrings:messaging` is present. Unit tests use the in-memory enrollment database and a fake sender.

The handler commits outside the transport transaction (`TransactionScopeOption.Suppress` and a new `DbContext`). The first commit inserts `inprogress`. The second updates that row to `completed`. A poll can see `404`, then `inprogress`, then `completed`. On the immediate path those two commits are back to back, so the page usually goes from `404` to `completed`. The one-minute delay is what makes the wait visible.

A second delivery of the same tracking id is safe. A completed row is left as it is.

## Stop processing

App Configuration seeds a feature flag named `StopProcessing`, off by default. While it is on, a pipeline behavior does **not** run the handler and does **not** move the message to the error queue. It sends the same message back to this endpoint with a 30 second delay (`RouteToThisEndpoint` + `DelayDeliveryWith`). Service Bus holds that scheduled message. Clearing the flag lets the next delivery run the handler. No process restart is required.

`HandleCurrentMessageLater` would retry immediately and spin. Failing the message would use the error queue. Aborting the pipeline without a send would drop the message.

The endpoint has no HTTP requests, so a hosted service refreshes App Configuration every 5 seconds. The API still refreshes through middleware as well.

## ServicePulse

The AppHost runs the Particular platform on the same Service Bus namespace as the endpoints. Enrollment rows stay in SQL. `AddDefaultComponents` adds:

| Resource | Role |
| --- | --- |
| `particular-error` | ServiceControl error instance. Reads the `error` queue. Heartbeats go to its input queue, `Particular.ServiceControl`. |
| `particular-audit` | ServiceControl audit instance. Reads the `audit` queue. |
| `particular-monitoring` | ServiceControl monitoring instance. Reads `Particular.Monitoring`. |
| `particular-servicepulse` | ServicePulse UI. |
| `particular-persistence` | RavenDB for ServiceControl. Enrollment rows stay in SQL. |

`Enrollment` audits each processed message, sends heartbeats, and sends metrics every 10 seconds. The send-only `Api` sends heartbeats. Metrics are not supported on a send-only endpoint.

`StopProcessing` does not fail the message, so held work does not appear under Failed Messages. While the flag is on, new audit records stop, heartbeats show the endpoint is still up, and the scheduled message waits in Service Bus. Turn the flag off and the next 30 second delivery is audited as a success. A real handler exception is what shows up as a failed message.

If no Particular license file is found, Aspire injects the placeholder `Trial`. ServiceControl then uses its own trial behavior. Put a `license.xml` where the host looks (`LocalApplicationData/ParticularSoftware` or `CommonApplicationData/ParticularSoftware`), or set `PARTICULARSOFTWARE_LICENSE`, to use a real license.

## Run

```bash
dotnet run --project src/AppHost
```

Open the **web** resource in the Aspire dashboard, and open **ServicePulse** from `particular-servicepulse`. Docker must be running. The first start pulls the Service Bus emulator, its SQL Server container, ServiceControl, ServicePulse, and RavenDB. The emulator listens on host ports `5672` and `5300`.

To pause handlers, enable `StopProcessing` in the emulator. The page will keep polling. Turn the flag off and the held messages are handled on their next 30 second delivery.

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
dotnet ef migrations add <Name> --project src/Enrollment --startup-project src/Enrollment --output-dir Migrations
```

Todos migrations stay in `src/Api`:

```bash
dotnet ef migrations add <Name> --project src/Api --startup-project src/Api
```

Startup calls `MigrateAsync` for each relational database. The NServiceBus installers create queue tables after that, when the connection string is present.
