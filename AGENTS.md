# Agent notes

Rules that are already true in this repo. Follow them when adding a feature.

## Names

- Message types live in `src/Messages`, namespace `Messages`. A command ends in `Command` and carries `[RouteTo("Endpoint")]`. An event ends in `Event` and has no send route.
- A handler is named after the message, marked `[Handler]`, and has one `Handle` method. Do not implement `IHandleMessages<T>`.
- The business class is the use case (`EnrollStudent`). The handler logs, calls it, and publishes only when the outcome says so.
- Each feature folder has `AutoConfigureServices : IFeatureHandlers`. Shared database and clients live in that feature's `Shared/` folder.
- `EnrollmentsDbContext`, `OrdersDbContext`, and `OrderRecord` stay in the `Enrollment` namespace so the existing EF snapshots stay valid.

## A new feature

- Add the folder. `Configure` registers that feature's services only. The default `Register` adds handlers in the marker's namespace, not the whole assembly.
- Reference the feature project from `src/Endpoint` and `src/Api` so the assembly is copied beside the host. Do not name feature types from the Endpoint project. Discovery loads project assemblies from the host deps file.
- A command's endpoint is `[RouteTo]`. An event handler's endpoint is `[HandleOn]`. When both attributes exist they must name the same endpoint. Do not call `AddAll`. One assembly may serve more than one endpoint.
- A gate the playground should show goes in `QueueGate.Endpoints`. A missing App Configuration key means `running`.

## Leave these alone

- `src/Messages`, `src/Routing`, and `src/Endpoint.Extensions` do not reference NServiceBus. `QueueGateBehavior` stays in `src/Endpoint`.
- Both transports use `TransportTransactionMode.ReceiveOnly`. Do not set `SendsAtomicWithReceive`, and do not add an outbox.
- Pause applies only to commands. An event is not paused. Pause resends with a 30 second delay and a new message id. Do not call `SetMessageId`.
- NServiceBus starts only when `ConnectionStrings:messaging` is present. Unit tests do not start it.
- The one-minute enrollment delay is `ProcessingModes.Delay`. Do not change it.
