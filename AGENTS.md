# AGENTS.md — mt-saga-order-processing

Guidance for AI coding agents working in this repository.

---

## Architecture at a Glance

Monorepo with four logical service boundaries coordinated by a MassTransit Saga:

```
OrderService (HTTP entry) → publishes EventContext<OrderCreated>
    → Saga (OrderStateMachine) → sends commands to worker queues
        → PaymentService consumer → publishes PaymentProcessed / PaymentFailed
        → InventoryService consumer → publishes InventoryReserved / InventoryFailed
    → Saga drives compensation on failure (RefundPayment → OrderCancelled)
```

Key projects:
- `src/MT.Saga.OrderProcessing.Contracts/` — shared events, commands, `EventContext<T>`, `OrderQueueNames`
- `src/MT.Saga.OrderProcessing.Saga/` — `OrderStateMachine`, `OrderState`
- `src/MT.Saga.OrderProcessing.Infrastructure/` — MassTransit config, EF Core, caching, persistence
- `src/Services/MT.Saga.OrderProcessing.OrderService/` — Minimal API entry-point (HTTP)
- `src/Services/MT.Saga.OrderProcessing.PaymentService/` — worker (IHostedService + consumers)
- `src/Services/MT.Saga.OrderProcessing.InventoryService/` — worker (IHostedService + consumers)
- `tests/MT.Saga.OrderProcessing.Tests/` — unit, integration, E2E (all in one project)

---

## Critical: EventContext Envelope

**Every message** on the bus is wrapped in `EventContext<TPayload>`, never sent as a raw event or command.

```csharp
// Correct
await publish.Publish(EventContext.Create(
    sourceService: OrderMessagingTopology.SourceService,
    entity: OrderMessagingTopology.EntityName,
    action: OrderMessagingTopology.Actions.Created,
    payload: new OrderCreated(orderId)), ct);

// Wrong — never publish raw payloads
await publish.Publish(new OrderCreated(orderId), ct);
```

Consumers always implement `IConsumer<EventContext<TPayload>>`.
`CorrelationId = OrderId` is resolved via `ctx.Message.Payload.OrderId` in the saga.

---

## Queue & Topology Constants

Queue names are centralised — never hardcode strings:

```csharp
OrderMessagingTopology.Queues.ProcessPayment   // "orders.process-payment-queue"
OrderMessagingTopology.Queues.ReserveInventory // "orders.reserve-inventory-queue"
OrderMessagingTopology.Queues.RefundPayment    // "orders.refund-payment-queue"
OrderMessagingTopology.Queues.Saga             // "orders.saga-queue"
OrderMessagingTopology.Queues.ReadModel        // "orders.read-model-queue"
OrderMessagingTopology.ExchangeName            // "orders.events-exchange" (topic type)
OrderMessagingTopology.Actions.*               // routing key segments
```

All events use a single fanout exchange (`orders.events-exchange`, type `topic`). Commands are routed via explicit queue URIs using `new Uri($"queue:{queueName}")`.

---

## Outbox Placement Rules

| Component | Uses EF Outbox? |
|---|---|
| OrderService (HTTP) | ❌ — uses `IPublishEndpoint` directly |
| Saga endpoint | ❌ — saga commands must reach workers deterministically |
| PaymentService consumer | ✅ — `UseEntityFrameworkOutbox<OrderSagaDbContext>` in `ConsumerDefinition` |
| InventoryService consumer | ✅ — same pattern |
| ReadModel projector | ❌ — retry-only, no inbox/outbox middleware |

Worker services register outbox in `ConsumerDefinition.ConfigureConsumer`, not in `AddWorkerServiceMassTransit`.

---

## Saga Rules

- Always inherit from `MassTransitStateMachine<TState>`
- Always call `SetCompletedWhenFinalized()`
- Correlate by `OrderId`: `e.CorrelateById(ctx => ctx.Message.Payload.OrderId)`
- Send commands via explicit queue URI: `CreateQueueUri(OrderQueueNames.ProcessPayment)`
- Only the Saga triggers compensation — consumers never decide flow
- `RegisterCommandEndpointConventions()` must be called once (idempotent via `Interlocked.Exchange`)

---

## Feature Folder Pattern (OrderService)

Feature folders are use-case slices; namespaces reflect folder paths:

```
Features/Orders/CreateOrder/
  CreateOrderCommand.cs          // sealed record, no DataAnnotation attributes
  CreateOrderCommandValidator.cs // sealed class : AbstractValidator<CreateOrderCommand>
  CreateOrderEndpoint.cs         // static class with Map(WebApplication app)
  CreateOrderResponse.cs         // sealed record
```

- POST/PUT/DELETE → `*Command` + `*CommandValidator`
- GET → `*Query` + `*QueryValidator`
- All HTTP output → `*Response` (never `*Dto`)
- Endpoints call `pipeline.ExecuteAsync(command, ct, async () => { ... })` for cross-cutting concerns

---

## Endpoint Pipeline

Behaviours execute in order: **Logging → Validation → [CacheInvalidation | Caching] → Handler**

```csharp
// Registration (ServiceCollectionExtensions.cs)
services.AddScoped(typeof(IEndpointBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IEndpointBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped<IEndpointBehavior<CreateOrderCommand, IResult>, CacheInvalidationBehavior<CreateOrderCommand, IResult>>();
services.AddScoped<IEndpointBehavior<GetOrderByIdQuery, IResult>, CachingBehavior<GetOrderByIdQuery, IResult>>();
services.AddScoped(typeof(EndpointPipeline<,>));
```

`ValidationException` is handled globally by `ValidationExceptionHandler` — never catch it inside endpoints.

---

## Caching

Always inject `ICacheService` (never `IFusionCache` directly). Cache tag constants live in `CacheTags`:

```csharp
await _cache.RemoveByTagAsync(CacheTags.Orders, ct);
```

Register via `services.AddOrderProcessingCaching(configuration)` (backed by FusionCache + Redis backplane).

---

## Developer Workflows

```powershell
pwsh ./dev.ps1 up       # Start .NET Aspire AppHost (RabbitMQ + PostgreSQL + Redis via Docker)
pwsh ./dev.ps1 down     # Stop AppHost
pwsh ./dev.ps1 build    # dotnet build --configuration Release
pwsh ./dev.ps1 test     # dotnet test (Testcontainers auto-provisions dependencies)
pwsh ./dev.ps1 migrate  # dotnet ef database update for OrderSagaDbContext
```

Tests are self-contained via Testcontainers — no running infrastructure needed for `dotnet test`.

---

## Testing Patterns

**State machine unit tests** (`OrderStateMachineTests.cs`): use `AddMassTransitTestHarness` with `InMemoryRepository`, swap consumers via `FailingXConsumer` inner classes with `ConsumerDefinition` that sets `EndpointName`.

**E2E tests** (`FullSagaE2EFixture.cs`): spin up real PostgreSQL, RabbitMQ, Redis via Testcontainers; boot `OrderService` via `WebApplicationFactory<OrderServiceEntryPoint>`; start Payment and Inventory workers as `IHost` instances.

```csharp
// Assert event published
(await harness.Published.Any<EventContext<OrderConfirmed>>(
    x => x.Context.Message.Payload.OrderId == orderId, ct)).ShouldBeTrue();
```

Always use `Shouldly` for assertions. Access test cancellation token via `TestContext.Current.CancellationToken`.

---

## Build Constraints

- `TreatWarningsAsErrors=true` in `Directory.Build.props` — all warnings are errors
- Central Package Management (CPM): versions only in `Directory.Packages.props`, never in `.csproj`
- Target framework: `net10.0`; nullable enabled; implicit usings enabled
- Maintenance scripts: `.ps1` (PowerShell 7+, cross-platform); `.sh` only for Docker container scripts

---

## Producer Interface Rules

| Scenario | Use |
|---|---|
| HTTP endpoint publishing event | `IPublishEndpoint` |
| Consumer publishing event | `ConsumeContext.Publish` |
| Sending command to queue | `ISendEndpointProvider` or `ConsumeContext.Send` |
| Never use | `IBus` as application dependency |

Publishing uses `PublishEventContextWithRetryAsync` extension which sets the routing key from `TopicRoutingKeyHelper.GenerateRoutingKey(sourceService, entity, action)`.

---

## Key Reference Files

| Purpose | File |
|---|---|
| Saga state machine | `src/MT.Saga.OrderProcessing.Saga/OrderStateMachine.cs` |
| Queue topology constants | `src/MT.Saga.OrderProcessing.Infrastructure/Messaging/OrderMessagingTopology.cs` |
| MassTransit DI registration | `src/MT.Saga.OrderProcessing.Infrastructure/Messaging/Configuration/SagaOrchestrationMassTransitExtensions.cs` |
| Consumer retry + outbox pattern | `src/Services/MT.Saga.OrderProcessing.PaymentService/Consumers/Definitions/ProcessPaymentConsumerDefinition.cs` |
| E2E test fixture | `tests/MT.Saga.OrderProcessing.Tests/E2E/Abstractions/FullSagaE2EFixture.cs` |
| Endpoint pipeline registration | `src/Services/MT.Saga.OrderProcessing.OrderService/Extensions/ServiceCollectionExtensions.cs` |
| Detailed messaging decisions | `docs/MASSTRANSIT_KB.md` |

