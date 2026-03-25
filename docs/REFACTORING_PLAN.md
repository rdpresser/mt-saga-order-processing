# MT.Saga.OrderProcessing - Refactoring Analysis & Plan

**Prepared**: March 25, 2026  
**Goal**: Eliminate reflection, improve explicit configuration, rationalize prefetch, simplify with extension methods for future componentization.

---

## Executive Summary

Current state analysis reveals:
- ✅ Good foundation with Saga pattern, outbox pattern, state machines
- ⚠️ **Issue 1**: Reflection-based consumer discovery via `AddConsumer(type)` in for-loop
- ⚠️ **Issue 2**: EventContext wrapper adds indirection without clear benefit (use direct message contracts)
- ⚠️ **Issue 3**: Prefetch hardcoded based on configuration without rationalization
- ⚠️ **Issue 4**: Extensions exist but spread across files (not organized for future componentization)
- ⚠️ **Issue 5**: State machine Send/Publish calls are repetitive (boilerplate for EventContext.Create)

---

## Issues Identified

### 1. Reflection-Based Consumer Registration

**Current Code**:
```csharp
// In MassTransitServiceCollectionExtensions.AddWorkerMassTransit()
foreach (var consumerType in consumerTypes)
{
    x.AddConsumer(consumerType);  // ← Reflection magic
}

foreach (var consumerType in consumerTypes)
{
    cfg.ReceiveEndpoint(ResolveWorkerEndpointName(consumerType), endpoint =>
    {
        //...
        endpoint.ConfigureConsumer(context, consumerType);  // ← More reflection
    });
}
```

**Problem**:
- Uses `AddConsumer(Type)` reflection overload
- Endpoint name resolved via switch/default logic
- Difficult to debug, trace, or customize per consumer
- Not clear what endpoints will be created at compile time

**Solution**:
- Remove `params Type[] consumerTypes` parameter
- Create explicit extension methods or factory for each service's consumers
- Define consumers as `IRegistrationConfigurator` extensions

---

### 2. EventContext Wrapper Indirection

**Current Code**:
```csharp
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public Event<EventContext<OrderCreated>> OrderCreated { get; private set; }
    // ...

    Initially(
        When(OrderCreated)
            .Send(ctx => EventContext.Create(
                sourceService: "orders",
                entity: "order",
                action: "process-payment",
                payload: new ProcessPayment(ctx.Message.Payload.OrderId),
                // ...
            ))
    );
}
```

**Problem**:
- `EventContext<T>` wraps actual message contract (adds layer of indirection)
- Saga must construct `EventContext` wrappers (boilerplate)
- Consumers receive `EventContext<PaymentProcessed>`, unwrap to get `PaymentProcessed`
- MassTransit KB recommends direct message contracts, not wrapper patterns

**Solution**:
- Use direct message contracts: `Event<OrderCreated>` instead of `Event<EventContext<OrderCreated>>`
- Remove `EventContext` wrapper from events
- Metadata/tracing/causation → use MassTransit headers (CorrelationId, RequestId, etc.)

---

### 3. Prefetch Configuration Without Rationalization

**Current Code**:
```csharp
private static void ConfigureCommonReceiveEndpointPolicies(...)
{
    var resilienceOptions = configuration.GetSection("Messaging:Resilience")
        .Get<MessagingResilienceOptions>()
        ?? new MessagingResilienceOptions();

    endpoint.PrefetchCount = (ushort)Math.Max(1, resilienceOptions.PrefetchCount);
    // ...
}
```

**Current Config**:
- `Messaging:Resilience:PrefetchCount` may be set to arbitrary values (16, 32, 64)
- No documentation about why it's lowered
- No distinction between prefetch (broker-side QoS) vs ConcurrentMessageLimit (app-side)

**Problem**:
- KB recommends: Keep prefetch at default ~64, use ConcurrentMessageLimit for app-level control
- Current approach may unnecessarily lower throughput

**Solution**:
- Document prefetch rationale
- Set prefetch to 64 (default) unless bottleneck proven
- Add explicit ConcurrentMessageLimit configuration
- Add comments explaining the distinction

---

### 4. Extensions Not Organized for Componentization

**Current Files**:
```
Infrastructure/Messaging/
├── TopicFanoutMassTransitExtensions.cs
├── TopicRoutingKeyHelper.cs
├── OrderMessagingTopology.cs
├── DependencyInjection/
│   └── MassTransitServiceCollectionExtensions.cs (200+ lines)
└── Observers/
```

**Problem**:
- Main extension method is 200+ lines in single file
- No clear separation for future independent components
- Consumers configuration mixed with topology, saga, observers

**Solution**:
- Create modular extensions:
  - `AddOrderSagaConfiguration()` (saga + state machine)
  - `AddPaymentServiceConsumers()` (payment consumers)
  - `AddInventoryServiceConsumers()` (inventory consumers)
  - `AddCommonMassTransitPolicies()` (retry, outbox, prefetch)

---

### 5. Repetitive EventContext Wrapping in State Machine

**Current Code**:
```csharp
.Send(ctx => EventContext.Create(
    sourceService: "orders",
    entity: "order",
    action: "process-payment",
    payload: new ProcessPayment(ctx.Message.Payload.OrderId),
    correlationId: ctx.Message.CorrelationId,
    causationId: ctx.Message.EventId.ToString(),
    userId: ctx.Message.UserId,
    isAuthenticated: ctx.Message.IsAuthenticated,
    version: ctx.Message.Version,
    metadata: ctx.Message.Metadata))
```

**Problem**:
- Repeated 5+ times in state machine
- 10-line boilerplate per Send/Publish call
- Difficult to maintain
- Not idiomatic MassTransit

**Solution**:
- Remove EventContext wrapper
- Use MassTransit's built-in message initialization: `Send<T>(new { })`
- Leverage automatic correlation ID propagation

---

## Refactoring Plan

### Phase 1: Simplify Message Contracts (Remove EventContext)

**Steps**:
1. In `MT.Saga.OrderProcessing.Contracts`:
   - Remove `EventContext<T>` wrapper from events
   - Use direct message records: `OrderCreated`, `PaymentProcessed`, etc.
   - Keep metadata in optional command headers (CorrelationId, etc.)

2. Update `OrderStateMachine`:
   - Change `Event<EventContext<OrderCreated>>` → `Event<OrderCreated>`
   - Simplify event definitions
   - Update event initialization

3. Update all consumers to receive unwrapped messages

### Phase 2: Reorganize Extensions for Componentization

**Steps**:
1. Split `MassTransitServiceCollectionExtensions` into:
   - `AddOrderSagaConfiguration()` → Saga + state machine only
   - `AddPaymentServiceConfiguration()` → Payment workers
   - `AddInventoryServiceConfiguration()` → Inventory workers
   - `AddCommonMassTransitPolicies()` → Shared retry, outbox, observers

2. Create new folder structure:
   ```
   Infrastructure/Messaging/Configuration/
   ├── SagaConfiguration.cs
   ├── PaymentServiceConfiguration.cs
   ├── InventoryServiceConfiguration.cs
   ├── CommonPoliciesConfiguration.cs
   └── TopologyConfiguration.cs
   ```

3. Organize PrefetchConfiguration separately

### Phase 3: Rationalize Prefetch & Config

**Steps**:
1. Document prefetch rationale in comments
2. Set prefetch to 64 (MassTransit default)
3. Add explicit ConcurrentMessageLimit (app-side control)
4. Remove arbitrary configuration adjustments
5. Create `MassTransitResilienceOptions`:
   ```csharp
   public class MassTransitResilienceOptions
   {
       // Prefetch: Keep at default unless bottleneck proven
       public int PrefetchCount { get; set; } = 64;  // RabbitMQ QoS

       // App-level concurrency limit
       public int ConcurrentMessageLimit { get; set; } = 20;

       // Retry policy
       public int MaxRetryAttempts { get; set; } = 5;
   }
   ```

### Phase 4: Explicit Consumer Registration

**Steps**:
1. Remove `params Type[] consumerTypes` from AddWorkerMassTransit
2. Create explicit registration methods:
   ```csharp
   public static IServiceCollection AddPaymentServiceConsumers(
       this IRegistrationConfigurator cfg)
   {
       cfg.AddConsumer<ProcessPaymentConsumer>();
       cfg.AddConsumer<RefundPaymentConsumer>();
       return cfg;
   }
   ```

3. Update OrderService/PaymentService/InventoryService Program.cs to use explicit registrations

### Phase 5: Simplify State Machine

**Steps**:
1. Remove EventContext wrapper from state machine events/sends
2. Simplify Send/Publish calls
3. Use MassTransit message initialization
4. Add helper extensions if needed

---

## Implementation Roadmap

| Phase | Item | File(s) | Priority | Est. Lines |
|-------|------|---------|----------|-----------|
| 1 | Remove EventContext from contracts & update consumers | Contracts/*.cs | HIGH | 50-100 |
| 2 | Split MassTransitServiceCollectionExtensions | Messaging/Configuration/* | HIGH | 300-400 |
| 3 | Update state machine to use direct messages | OrderStateMachine.cs | HIGH | -50 (reduction) |
| 4 | Rationalize prefetch config | Messaging/Configuration/CommonPoliciesConfiguration.cs | MEDIUM | 50-100 |
| 5 | Remove reflection-based consumer registration | Messaging/DependencyInjection/* | HIGH | -100 (reduction) |
| 6 | Create explicit consumer registration extensions | Services/*/Program.cs | HIGH | 30-50 per service |
| **Total** | | | | ~1000 lines changed |

---

## Expected Benefits

### After Refactoring

✅ **Clarity**: No reflection magic; explicit consumer registration at compile time  
✅ **Maintainability**: Each service defines its own consumers clearly  
✅ **Performance**: Proper prefetch rationalization (throughput maintained, clarity improved)  
✅ **Componentization**: Extensions organized for future independent package/service reuse  
✅ **Simplicity**: Fewer wrapper layers; direct message contracts  
✅ **Testability**: Explicit dependencies easier to test  
✅ **Future-ready**: When moving to distributed services, extensions can be easily packaged

---

## Testing Strategy

1. **Unit Tests**: Verify consumer logic remains unchanged post-refactoring
2. **Integration Tests**: Run E2E Saga flow (order → payment → inventory → confirmation)
3. **Configuration Tests**: Verify correct endpoints created with explicit registration
4. **Compilation**: Ensure no missing types after EventContext removal
5. **Smoke Tests**: Quick validation with sample messages

---

## Risk Mitigation

- **EventContext removal**: Backwards compatibility with existing messages? → Check if external systems depend on EventContext wrapper. If not, safe to remove.
- **Consumer registration changes**: Ensure all consumers still picked up correctly
- **Prefetch rationalization**: May need tuning based on actual DB load; keep monitoring
- **Extension organization**: Ensure no cross-service circular dependencies

---

## Sign-Off

**Reviewer**: Development Team  
**Date**: TBD  
**Status**: READY FOR IMPLEMENTATION

