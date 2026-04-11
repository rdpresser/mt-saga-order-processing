# CLAUDE.md — mt-saga-order-processing

Full architectural reference: [AGENTS.md](AGENTS.md) — read it before making structural changes.

## What This Project Is

MassTransit Saga orchestration system on .NET 10. An `OrderStateMachine` coordinates Payment and Inventory workers via RabbitMQ. PostgreSQL persists saga state and a read model. FusionCache + Redis provide low-latency reads.

```
OrderService (HTTP) → Saga (OrderStateMachine) → PaymentService / InventoryService
                    ↓ compensation on failure (RefundPayment → OrderCancelled)
```

---

## Critical Rules (never violate)

### EventContext envelope
Every bus message is `EventContext<TPayload>`. Never publish raw events or commands.

```csharp
// Correct
await publish.Publish(EventContext.Create(
    sourceService: OrderTopologyConstants.SourceService,
    entity: OrderTopologyConstants.EntityName,
    action: OrderTopologyConstants.EventActions.Created,
    payload: new OrderCreated(orderId)), ct);

// Wrong
await publish.Publish(new OrderCreated(orderId), ct);
```

Consumers implement `IConsumer<EventContext<TPayload>>`.
Saga correlates via `ctx.Message.Payload.OrderId`, not `ctx.Message.CorrelationId`.

### Circular dependency (project references)
```
Contracts          ← no upstream dependencies
Saga               → Contracts only
Infrastructure     → Contracts + Saga
OrderService       → Infrastructure + Contracts
PaymentService     → Infrastructure + Contracts
InventoryService   → Infrastructure + Contracts
```
`Saga` must never reference `Infrastructure`.

### Topology constants — never hardcode strings
- Queue names: `OrderQueueNames.*` in `Contracts/`
- Routing key strings: `OrderTopologyConstants.*` in `Contracts/`
- Full topology config: `OrderMessagingTopology` in `Infrastructure/` (delegates to the above)

### Outbox placement
- PaymentService and InventoryService: EF Outbox inside `ConsumerDefinition.ConfigureConsumer`
- Saga, OrderService, ReadModel projector: no outbox (intentional — see AGENTS.md)

### Saga orchestration
- Only `OrderStateMachine` drives compensation and state transitions
- Consumers react and publish results — they never decide workflow flow
- Always call `SetCompletedWhenFinalized()`
- Commands sent via explicit queue URIs: `new Uri($"queue:{OrderQueueNames.ProcessPayment}")`

---

## Test Stack

| Tool | Usage |
|---|---|
| xUnit v3 | Test framework |
| Shouldly | All assertions — never `Assert.*` |
| `ITestHarness` | Consumer integration tests |
| `ISagaStateMachineTestHarness<OrderStateMachine, OrderState>` | State machine unit tests |
| Testcontainers | Real PostgreSQL, RabbitMQ, Redis in E2E tests |
| `WebApplicationFactory<OrderServiceEntryPoint>` | OrderService HTTP integration tests |

Always use `TestContext.Current.CancellationToken` — never `CancellationToken.None`.

Finalization assertion: `(await sagaHarness.NotExists(orderId, ct)).ShouldBeNull()`.

---

## Dev Commands

```powershell
pwsh ./dev.ps1 up       # Start Aspire AppHost (RabbitMQ, PostgreSQL, Redis via Docker)
pwsh ./dev.ps1 build    # dotnet build --configuration Release
pwsh ./dev.ps1 test     # dotnet test (Testcontainers auto-provisions dependencies)
pwsh ./dev.ps1 down     # Stop AppHost
pwsh ./dev.ps1 migrate  # dotnet ef database update for OrderSagaDbContext
```

---

## Build Constraints

- `TreatWarningsAsErrors=true` — all warnings are compile errors
- Central Package Management (CPM): versions only in `Directory.Packages.props`, never in `.csproj`
- Target framework: `net10.0`; nullable enabled; implicit usings enabled
- Scripts: `.ps1` (PowerShell 7+, cross-platform); `.sh` only for Docker container scripts

---

## Feature Folder Pattern (OrderService)

Each use case is a self-contained vertical slice:

```
Features/Orders/CreateOrder/
  CreateOrderCommand.cs          # sealed record, no DataAnnotation attributes
  CreateOrderCommandValidator.cs # sealed class : AbstractValidator<CreateOrderCommand>
  CreateOrderEndpoint.cs         # static class with Map(WebApplication app)
  CreateOrderResponse.cs         # sealed record
```

- Never use `Dto` suffix — use `Command`, `Query`, or `Response`
- `ValidationException` propagates to global handler — never catch inside endpoints
- Caching: inject `ICacheService`, never `IFusionCache` directly

---

## Code Style

- DDD-light — no formal aggregates or value objects; pragmatic feature folders
- Prefer editing existing files over creating new ones
- No speculative abstractions — match the complexity the task actually requires
- `TreatWarningsAsErrors` means no unused variables, no nullable warnings, no missing XML docs on public APIs if required
