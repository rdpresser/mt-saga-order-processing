# MassTransit Configuration Refactoring - Status

**Last Updated:** April 10, 2026  
**Objective:** Keep the MassTransit configuration explicit, modular, and aligned with the validated runtime behavior of the solution.

---

## Current Validated State

### ✅ Documentation

1. **MASSTRANSIT_KB.md** (600+ lines)
   - Comprehensive knowledge base from 28 official documentation URLs
   - Best practices, do's/don'ts
   - Explained: prefetch rationale, outbox pattern, saga state machine, consumers, retry policies
   - Repository decisions documented: queue naming, explicit queue URI routing, producer interface rules, outbox placement decisions, major discoveries from integration fixes

2. **REFACTORING_PLAN.md**
   - 5 issues identified with detailed analysis
   - 4-phase roadmap with clear deliverables
   - Benefits documented (clarity, extensibility, maintainability)

### ✅ Code Structure (Configuration Directory)

Created `/src/MT.Saga.OrderProcessing.Infrastructure/Messaging/Configuration/`:

- `CommonMassTransitPoliciesConfiguration.cs` - Centralized resilience policies (prefetch, retry, kill switch, outbox)
- `OrderSagaConfiguration.cs` - Explicit saga state machine + endpoint setup
- `SagaOrchestrationMassTransitExtensions.cs` - Main builder extensions
- `DatabaseAndPoliciesExtensions.cs` - DB context and options registration

### ✅ Modular Design

- No reflection in new classes
- Consumer definitions are the active runtime registration mechanism for worker endpoints
- Worker services register consumers explicitly in each `Program.cs` and materialize endpoints via `cfg.ConfigureEndpoints(context)`
- Unused placeholder helper files were removed to keep code and docs aligned
- Configuration sections documented: `Messaging:RabbitMq`, `Messaging:Resilience`, `Database:Postgres`
- Observers registered for logging (LoggingConsumeObserver, LoggingPublishObserver)

### ✅ Integration Fixes Validated

1. **Saga orchestration service does not use bus outbox**
   - `AddEntityFrameworkOutbox` removed from `AddSagaOrchestrationMassTransit`
   - prevents HTTP-originated `OrderCreated` messages from being trapped without `SaveChanges`

2. **Saga receive endpoint does not use EF endpoint outbox**
   - `UseEntityFrameworkOutbox` removed from `OrderStateDefinition.ConfigureSaga`
   - prevents saga command sends from being buffered and never reaching workers in the integration runtime shape

3. **Worker services keep EF outbox + bus outbox**
   - durable publish behavior remains active where consumers have a real transactional boundary

4. **Read-model projector remains retry-only**
   - no inbox/outbox middleware on projector endpoint
   - avoids projection gaps caused by deduplication suppressing deliveries

5. **Command routing from saga is explicit**
   - saga uses `queue:orders.process-payment-queue`, `queue:orders.reserve-inventory-queue`, and `queue:orders.refund-payment-queue`
   - reduces dependence on static `EndpointConvention` state in tests

6. **Queue naming uses topology constants**
   - `OrderMessagingTopology.Queues.*` is the source of truth
   - raw endpoint strings such as `"process-payment"` are considered drift and should be replaced

7. **Full regression suite is green**
   - `dotnet test` passes with 135/135 tests (unit + integration + E2E via Testcontainers)

8. **Topology constants extracted to Contracts**
   - `OrderTopologyConstants` added to `Contracts/Messaging/` — all action/entity/source strings
   - `OrderStateMachine` replaced all hardcoded strings with `OrderTopologyConstants.*`
   - `OrderMessagingTopology` delegates to `OrderTopologyConstants` (no duplication)
   - Circular dependency constraint documented and enforced: `Saga → Contracts`, never `Saga → Infrastructure`

9. **Test suite expanded**
   - `OrderStateMachineTests`: saga finalization assertions via `sagaHarness.NotExists` on all terminal paths
   - `RefundPaymentConsumerIntegrationTests`: new file — fire-and-forget behavior, idempotency
   - `ProcessPaymentConsumerIntegrationTests`: `IConsumerTestHarness` assertion + transport-level correlation ID propagation test
   - `ReserveInventoryConsumerIntegrationTests`: `IConsumerTestHarness` assertion + explicit `false` header edge case
   - `OrderApiIntegrationTests`: replaced manual nested polling loop with `WaitForOrderReadModelStatusAsync`
   - `FullSagaE2EFixture`: extracted `PublishEventToExchangeAsync<TPayload>` generic helper — ~60 lines of duplication removed

---

## Documentation Rules Going Forward

1. When a major messaging discovery is validated, document it in `docs/MASSTRANSIT_KB.md`.
2. Keep this status file aligned with the real runtime architecture, not with superseded intermediary plans.
3. Do not leave placeholder patterns documented as active runtime behavior if the actual registration path differs.
4. Queue names, outbox placement, and routing authority changes must be reflected in docs in the same task.

---

## Open Follow-Up Items

1. Review older planning docs that still mention EventContext removal as a target, because the current decision is to keep `EventContext<T>`.
2. Keep README and other onboarding docs aligned with the validated outbox placement decisions.

## Configuration Schema (Current Reference)

Primary runtime sections currently used by services are:

- `Messaging:RabbitMq`
- `Messaging:Resilience`
- `Database:Postgres`

```json
{
  "Messaging": {
    "RabbitMq": {
      "Host": "localhost",
      "Port": 5672,
      "UserName": "guest",
      "Password": "guest",
      "VirtualHost": "/"
    },
    "Resilience": {
      "PrefetchCount": 16,
      "MaxRetryAttempts": 5,
      "PublishMaxAttempts": 3,
      "PublishRetryDelayMilliseconds": 200
    }
  },
  "Database": {
    "Postgres": {
      "Host": "localhost",
      "Port": 5432,
      "UserName": "postgres",
      "Password": "postgres",
      "Database": "mt_saga_order_processing",
      "MaintenanceDatabase": "postgres",
      "Schema": "public",
      "ConnectionTimeout": 30,
      "CommandTimeout": 30,
      "MinPoolSize": 5,
      "MaxPoolSize": 100,
      "KeepAlive": 30,
      "Multiplexing": false
    }
  }
}
```

---

## Validation Checklist

- [x] Current configuration files compile
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes with 135/135 tests
- [x] Worker services use explicit consumer registration with consumer definitions
- [x] Queue names are centralized in `OrderMessagingTopology.Queues`
- [x] Major outbox discoveries are documented in the KB
- [x] Remove unused placeholder helper files
- [ ] Review older docs for stale migration notes

---

## Design Validation (Quality Checks)

✅ **Explicit** - No magic reflection; methods clearly show what's registered  
✅ **Modular** - Consumer registration per service; policies centralized; endpoints explicit  
✅ **Documented** - XML docs on every public method; configuration sections explained  
✅ **Explicit Runtime Wiring** - Worker `Program.cs` files register concrete consumers and let definitions create endpoints  
✅ **Testable** - No static state; all config passed via DI  
✅ **Future-Ready** - Easy to add new services: either add new consumer definitions or implement service-specific registration extensions when they become real runtime entry points

---

## Reference Files

- **Knowledge Base:** `/docs/MASSTRANSIT_KB.md`
- **Refactoring Plan:** `/docs/REFACTORING_PLAN.md`
- **Worker runtime path (currently active):** `/src/MT.Saga.OrderProcessing.Infrastructure/Messaging/DependencyInjection/MassTransitServiceCollectionExtensions.cs`
- **Contracts (messages):** `/src/MT.Saga.OrderProcessing.Contracts/Commands/` and `/Events/`

---

## Status Summary

**Status:** ✅ **Validated** - Core configuration, topology constants, and runtime behavior are aligned with 135/135 passing tests  
**Complexity:** Low-Medium - Main work now is preventing documentation drift and adding missing features (CI, health checks, real worker logic)  
**Risk Level:** Low - All critical paths have test coverage; messaging docs updated to match current architecture
