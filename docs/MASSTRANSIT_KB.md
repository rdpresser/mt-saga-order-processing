# MassTransit Knowledge Base (KB) - Internal Reference

**Source**: [Official MassTransit Documentation](https://masstransit.io/documentation)
**Version**: MassTransit v8.x+
**Purpose**: Comprehensive internal reference for explicit configuration patterns, validated runtime decisions, and MassTransit-specific best practices.

This document is the detailed reference.

- `README.md` should stay as the executive summary
- this KB should hold the detailed rationale, repository decisions, and validated discoveries
- when a major messaging discovery is confirmed, update this KB in the same task

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Messages](#messages)
3. [Consumers](#consumers)
4. [Producers (Send/Publish)](#producers)
5. [Sagas](#sagas)
6. [Routing Slips & Activities](#routing-slips)
7. [Topology Configuration](#topology)
8. [Exception Handling & Retry](#exceptions)
9. [RabbitMQ Transport Specifics](#rabbitmq)
10. [Persistence (EF Core / PostgreSQL)](#persistence)
11. [Configuration Patterns](#config-patterns)
12. [Prefetch Rationalization](#prefetch)
13. [Definitions Catalog (MassTransit)](#definitions-catalog)
14. [Definitions Adoption for This Repository](#definitions-adoption)
15. [Repository Decisions](#repository-decisions)
16. [Known Discoveries](#known-discoveries)
17. [Documentation URLs by Topic](#documentation-urls)

---

## Core Concepts

### Messages

**Definition**: A message is a contract (record, class, or interface) that flows through the bus.

**Rules**:

- Use `records` with `{ get; init; }` accessors (immutable, recommended)
- Message type names must be fully qualified (namespace + type)
- Two projects must use the exact same namespace/name for a message to be recognized
- Messages should contain **state only**, no behavior

**Types**:

- **Commands** (verbs): `SubmitOrder`, `UpdateCustomer` → sent to specific queue
- **Events** (past tense): `OrderSubmitted`, `CustomerUpdated` → published to all subscribers

**Message Names Example**:

```csharp
// Command - verb/noun, imperative
public record SubmitOrder
{
    public Guid OrderId { get; init; }
    public DateTime OrderDate { get; init; }
}

// Event - noun/verb (past tense), declarative
public record OrderSubmitted
{
    public Guid OrderId { get; init; }
    public DateTime Timestamp { get; init; }
}
```

**Message Headers** (automatically managed):

- `MessageId`: Unique per message (NewId)
- `CorrelationId`: Links messages in a conversation
- `RequestId`: For request/response patterns
- `InitiatorId`: Tracks originating source
- `ConversationId`: Groups related messages
- `SourceAddress`: Where message originated
- `DestinationAddress`: Target endpoint
- `SentTime`: When sent (UTC)

**Correlation**: Messages are grouped by `CorrelationId` (either explicit or by convention):

```csharp
// Convention examples (auto-detected):
// 1. CorrelatedBy<Guid> interface
// 2. Property named: CorrelationId, CommandId, or EventId (Guid type)
// 3. Global topology configuration

// Explicit configuration:
GlobalTopology.Send.UseCorrelationId<SubmitOrder>(x => x.OrderId);
```

---

## Consumers

**Definition**: A consumer implements `IConsumer<T>` and handles one message type.

**Interface**:

```csharp
public interface IConsumer<in TMessage> : IConsumer
    where TMessage : class
{
    Task Consume(ConsumeContext<TMessage> context);
}
```

**Basic Consumer**:

```csharp
public sealed class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    private readonly IOrderRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;

    public SubmitOrderConsumer(IOrderRepository repository, IPublishEndpoint publishEndpoint)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        // ConsumeContext provides:
        // - context.Message (the actual message)
        // - context.Headers (message headers)
        // - context.CorrelationId (for tracing)
        // - context.Send/Publish (produce downstream messages)

        var order = await _repository.CreateAsync(context.Message, context.CancellationToken);

        await context.Publish<OrderSubmitted>(new
        {
            order.OrderId,
            context.Message.OrderDate
        }, context.CancellationToken);
    }
}
```

**Consumer Definition** (optional, for configuration):

```csharp
public sealed class SubmitOrderConsumerDefinition : ConsumerDefinition<SubmitOrderConsumer>
{
    public SubmitOrderConsumerDefinition()
    {
        EndpointName = "submit-order";
        ConcurrentMessageLimit = 4;  // per consumer instance
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SubmitOrderConsumer> consumerConfigurator)
    {
        endpointConfigurator.UseMessageRetry(r => r.Immediate(5));
        endpointConfigurator.UseInMemoryOutbox();
    }
}
```

**Batch Consumer** (for bulk processing):

```csharp
public sealed class OrderAuditBatchConsumer : IConsumer<Batch<OrderSubmitted>>
{
    public async Task Consume(ConsumeContext<Batch<OrderSubmitted>> context)
    {
        for (int i = 0; i < context.Message.Length; i++)
        {
            ConsumeContext<OrderSubmitted> message = context.Message[i];
            // Process batch...
        }
    }
}

// Configuration:
// services.AddConsumer<OrderAuditBatchConsumer>(c =>
// {
//     c.Options<BatchOptions>(x =>
//     {
//         x.MessageLimit = 100;
//         x.ConcurrentMessageLimit = 10;
//     });
// });
```

---

## Producers

### Send

**Definition**: Delivers a message to a **specific endpoint/queue** (one-to-one).

**Key Points**:

- Obtain endpoint from `ConsumeContext` (consumer), `ISendEndpointProvider`, or `IBus`
- Prefer closest scope (ConsumeContext > ISendEndpointProvider > IBus)
- With EF Outbox/Bus Outbox, prefer the scoped interfaces (`ConsumeContext`, `ISendEndpointProvider`)
- Always use async: `GetSendEndpoint`, then `Send`

**Example**:

```csharp
public sealed class PaymentProcessor
{
    private readonly ISendEndpointProvider _endpointProvider;
    private readonly Uri _refundServiceAddress;

    public PaymentProcessor(ISendEndpointProvider endpointProvider)
    {
        _endpointProvider = endpointProvider;
        _refundServiceAddress = new Uri("queue:refund-service");
    }

    public async Task SendRefundAsync(Guid orderId, decimal amount, CancellationToken ct)
    {
        var endpoint = await _endpointProvider.GetSendEndpoint(_refundServiceAddress);

        await endpoint.Send<RequestRefund>(new
        {
            OrderId = orderId,
            Amount = amount,
            __CorrelationId = orderId  // __ prefix sets header
        }, ct);
    }
}
```

**Short Addresses** (RabbitMQ):

- `queue:queue-name`
- `exchange:exchange-name`
- `topic:topic-name`

### Publish

**Definition**: Broadcasts a message to **all subscribers** (one-to-many).

**Key Points**:

- Used for events (past tense)
- Obtain from `ConsumeContext` (preferred), `IPublishEndpoint`, or `IBus`
- With EF Outbox/Bus Outbox, prefer `ConsumeContext` or `IPublishEndpoint` so the publish participates in the scoped outbox
- Avoid `IBus` as the default dependency for application code; use it only when global bus access is explicitly required
- Automatic topology creation for subscribers

**Example**:

```csharp
public sealed class OrderConsumer : IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        // Persist order...

        // Use ConsumeContext for publishing (closest scope)
        await context.Publish<OrderSubmitted>(new
        {
            context.Message.OrderId,
            Timestamp = DateTime.UtcNow
        }, context.CancellationToken);
    }
}
```

---

## Sagas

### State Machine Sagas (Recommended)

**Definition**: A saga is a long-lived, stateful orchestrator that:

1. Initiates from an event
2. Manages state across multiple events
3. Orchestrates compensation on failure

**Core Components**:

```csharp
// State (persistent data)
public sealed class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }  // Primary key
    public string CurrentState { get; set; }  // Or: int CurrentState
    public DateTime? OrderDate { get; set; }
    public byte[] RowVersion { get; set; }    // For optimistic concurrency (SQL)
}

// State Machine
public sealed class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    // States
    public State Submitted { get; private set; }
    public State Accepted { get; private set; }
    public State Completed { get; private set; }

    // Events
    public Event<SubmitOrder> SubmitOrder { get; private set; }
    public Event<OrderAccepted> OrderAccepted { get; private set; }
    public Event<OrderCompleted> OrderCompleted { get; private set; }

    public OrderStateMachine()
    {
        // Configure state storage
        InstanceState(x => x.CurrentState);

        // Declare events
        Event(() => SubmitOrder, e => e.CorrelateById(x => x.Message.OrderId));
        Event(() => OrderAccepted, e => e.CorrelateById(x => x.Message.OrderId));
        Event(() => OrderCompleted, e => e.CorrelateById(x => x.Message.OrderId));

        // Define behavior using Initially/During/DuringAny
        Initially(
            When(SubmitOrder)
                .Then(context => context.Saga.OrderDate = context.Message.OrderDate)
                .TransitionTo(Submitted)
        );

        During(Submitted,
            When(OrderAccepted)
                .TransitionTo(Accepted),
            When(OrderCompleted)
                .Finalize()
        );

        During(Accepted,
            When(OrderCompleted)
                .Finalize()
        );

        // Handle finalization
        SetCompletedWhenFinalized();
    }
}
```

**Key Patterns**:

1. **Initiating Events** (Initial state):

   ```csharp
   Initially(
       When(SubmitOrder)
           .Then(context => context.Saga.CustomerNumber = context.Message.CustomerNumber)
           .TransitionTo(Submitted)
   );
   ```

2. **Handling Events** (During specific states):

   ```csharp
   During(Submitted,
       When(OrderAccepted)
           .Then(context => context.Saga.AcceptedAt = DateTime.UtcNow)
           .TransitionTo(Accepted)
   );
   ```

3. **Missing Instance Handling**:

   ```csharp
   Event(() => OrderCancellationRequested, e =>
   {
       e.CorrelateById(context => context.Message.OrderId);
       e.OnMissingInstance(m =>
           m.ExecuteAsync(x => x.RespondAsync<OrderNotFound>(new { x.OrderId }))
       );
   });
   ```

4. **Saga Completion**:

   ```csharp
   DuringAny(
       When(OrderCompleted)
           .Finalize()
   );

   SetCompletedWhenFinalized();  // Remove from repository
   ```

### Saga Configuration

**Registration**:

```csharp
services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
            {
                builder.UseNpgsql(connectionString, m =>
                {
                    m.MigrationsAssembly(typeof(Program).Assembly.GetName().Name);
                });
            });
            r.UsePostgres();
        });
});
```

---

## Routing Slips

### Concepts

**Routing Slip**: A message that flows through a series of **activities**, each performing work and optionally compensating on failure.

**Activity Interface**:

```csharp
// Compensating activity (supports undo)
public interface IActivity<TArguments, TLog>
{
    Task<ExecutionResult> Execute(ExecuteContext<TArguments> context);
    Task<CompensationResult> Compensate(CompensateContext<TLog> context);
}

// Execute-only activity (no compensation)
public interface IExecuteActivity<TArguments>
{
    Task<ExecutionResult> Execute(ExecuteContext<TArguments> context);
}
```

**Activity Implementation**:

```csharp
public sealed class DownloadImageActivity : IActivity<DownloadImageArguments, DownloadImageLog>
{
    public async Task<ExecutionResult> Execute(ExecuteContext<DownloadImageArguments> context)
    {
        var args = context.Arguments;
        string imagePath = Path.Combine(args.WorkPath, context.TrackingNumber.ToString());

        try
        {
            await DownloadImage(args.ImageUri, imagePath);

            return context.Completed<DownloadImageLog>(new { ImagePath = imagePath });
        }
        catch (Exception ex)
        {
            return context.Faulted();  // or throw ex
        }
    }

    public async Task<CompensationResult> Compensate(CompensateContext<DownloadImageLog> context)
    {
        // Undo: delete the downloaded file
        File.Delete(context.Log.ImagePath);
        return context.Compensated();
    }
}
```

**Building a Routing Slip**:

```csharp
var builder = new RoutingSlipBuilder(NewId.NextGuid());

builder.AddActivity("DownloadImage", new Uri("rabbitmq://localhost/execute_downloadimage"), new
{
    ImageUri = new Uri("http://example.com/image.jpg")
});

builder.AddActivity("ProcessImage", new Uri("rabbitmq://localhost/execute_processimage"));

builder.AddVariable("WorkPath", @"\work\path");

// Subscribe for events
builder.AddSubscription(new Uri("rabbitmq://localhost/routing-slip-monitor"),
    RoutingSlipEvents.All);

var routingSlip = builder.Build();

await bus.Execute(routingSlip);
```

---

## Topology Configuration

### Concepts

**Topology**: How message types map to broker entities (exchanges/topics, queues, bindings).

**Three Topologies**:

1. **Send Topology**: Routes commands to specific queues
2. **Publish Topology**: Maps events to exchanges/topics
3. **Consume Topology**: Subscribes queues to exchanges/topics

### Message Attributes

```csharp
[EntityName("order-submitted")]
[ConfigureConsumeTopology(true)]  // Create exchange/topic for this message
public record OrderSubmitted
{
    public Guid OrderId { get; init; }
}

[ExcludeFromTopology]  // Don't create exchange for base types
public interface ICommand { }

[ExcludeFromImplementedTypes]  // Don't create scope filters
public interface IEvent { }
```

### Explicit Configuration

**RabbitMQ Exchanges**:

```csharp
x.UsingRabbitMq((context, cfg) =>
{
    // Configure publish (exchange) settings
    cfg.Publish<OrderSubmitted>(x =>
    {
        x.Durable = true;        // Survive broker restart
        x.AutoDelete = false;     // Keep after last binding removed
        x.ExchangeType = "fanout"; // fanout, direct, topic, headers
    });

    // Configure send (routing key) settings
    cfg.Send<ProcessOrder>(x =>
    {
        x.UseRoutingKeyFormatter(ctx => ctx.Message.Priority.ToString());
        x.UseCorrelationId(ctx => ctx.Message.OrderId);
    });

    // Configure receive endpoint topology
    cfg.ReceiveEndpoint("order-queue", e =>
    {
        e.Bind<OrderSubmitted>();  // Subscribe to message type
        e.Bind("legacy-exchange");   // Bind to specific exchange

        e.ConfigureConsumer<OrderConsumer>(context);
    });

    cfg.ConfigureEndpoints(context);
});
```

### Global Topology

```csharp
// Set globally for all bus instances
GlobalTopology.Send.UseCorrelationId<SubmitOrder>(x => x.OrderId);

GlobalTopology.Send.TryAddConvention(new RoutingKeySendTopologyConvention());
GlobalTopology.Send.UseRoutingKeyFormatter<IHasRoutingKey>(x => x.RoutingKey.ToString());
```

---

## Exception Handling & Retry

### Retry Policies

**Available Policies**:

- `Immediate(count)`: Retry immediately, N times
- `Interval(count, interval)`: Retry after fixed delay
- `Intervals(params)`: Retry after custom delays each time
- `Exponential(retries, min, max, delta)`: Exponential backoff
- `Incremental(retries, min, delta)`: Linear increasing delays

**Example**:

```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.AddConfigureEndpointsCallback((context, name, cfg) =>
    {
        cfg.UseMessageRetry(r =>
        {
            r.Immediate(5);  // 5 immediate retries
            r.Interval(3, TimeSpan.FromSeconds(10));  // 3 retries after 10s each
        });
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

### Exception Filters

```csharp
cfg.UseMessageRetry(r =>
{
    r.Handle<ArgumentNullException>();
    r.Handle<TimeoutException>();
    r.Ignore<ArgumentException>(ex => ex.ParamName == "orderTotal");
    r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));
});
```

### Redelivery (Second-Level Retry)

```csharp
cfg.UseDelayedRedelivery(r =>
	r.Intervals(
		TimeSpan.FromMinutes(5),
		TimeSpan.FromMinutes(15),
		TimeSpan.FromMinutes(30)
	)
);

cfg.UseMessageRetry(r => r.Immediate(5));
```

### Outbox Pattern

Prevents duplicate messages on retry by buffering sends until consumer succeeds:

```csharp
cfg.ReceiveEndpoint("submit-order", e =>
{
    e.UseMessageRetry(r => r.Immediate(5));
    e.UseInMemoryOutbox();  // In-memory (fast, per-instance)
    // or e.UseEntityFrameworkOutbox<DbContext>();  // Durable

    e.ConfigureConsumer<SubmitOrderConsumer>(context);
});
```

### Scoped Producer Guidance with Outbox

When Entity Framework Outbox or Bus Outbox is enabled, the producer interface choice matters:

- `ConsumeContext` is preferred inside consumers and sagas
- `IPublishEndpoint` is preferred in HTTP/application services for event publishing
- `ISendEndpointProvider` is preferred in HTTP/application services for direct queue sends
- `IBus` should not be the default dependency because it bypasses the scoped outbox behavior

Repository rule:

```text
Events   -> IPublishEndpoint (or ConsumeContext.Publish inside consumers)
Commands -> ISendEndpointProvider (or ConsumeContext.Send inside consumers)
IBus     -> only when global bus access is intentionally required
```

### Fault Handling

```csharp
public sealed class OrderFaultConsumer : IConsumer<Fault<SubmitOrder>>
{
    private readonly ILogger<OrderFaultConsumer> _logger;

    public async Task Consume(ConsumeContext<Fault<SubmitOrder>> context)
    {
        _logger.LogError("Order failed: {OrderId}",
            context.Message.Message.OrderId);

        foreach (var exception in context.Message.Exceptions)
        {
            _logger.LogError("Exception: {ExceptionType}: {Message}",
                exception.ExceptionType, exception.Message);
        }

        // Handle fault...
    }
}
```

---

## RabbitMQ Transport Specifics

### Prefetch Count

**Definition**: The number of messages the consumer fetches from the broker **before fully acknowledging any**.

**Default Prefetch**: `64` per transport specification

**Key Rationales**:

- **Higher prefetch**: Better throughput but requires more memory and increases in-flight messages
- **Lower prefetch**: Lower latency per message but reduces throughput
- **Database workload**: If database is the bottleneck, lower prefetch (e.g., 16-32) limits concurrent DB operations
- **Network latency**: High-latency networks benefit from higher prefetch

**Recommendation**:

- Start with **default (64)**
- Lower only if database/resource bottleneck exists
- Per-message processing latency should not drive prefetch down
- Use **ConcurrentMessageLimit** (consumer level) for fine-grained control

**Configuration**:

```csharp
x.UsingRabbitMq((context, cfg) =>
{
    cfg.ReceiveEndpoint("order-queue", e =>
    {
        e.PrefetchCount = 64;  // RabbitMQ QoS: messages pre-fetched
        e.ConcurrentMessageLimit = 20;  // App-level limit (subset of prefetch)
    });
});
```

### Exchanges & Routing

**Exchange Types**:

- `fanout`: All messages to all bound queues
- `direct`: Routing key must match exactly
- `topic`: Routing key pattern matching (`order.*`, `*.shipped`)
- `headers`: Route by message headers

**Topology in RabbitMQ**:

- Publishing: Sends to **exchange** matching message type
- Consuming: **Queue** binds to exchange with routing key
- MassTransit creates exchanges and bindings automatically

### Topology Best Practices for RabbitMQ

```csharp
cfg.UsingRabbitMq((context, cbfg) =>
{
    // Publish exchanges
    cbfg.Publish<OrderSubmitted>(x =>
    {
        x.ExchangeType = ExchangeType.Fanout;
        x.Durable = true;
    });

    // Send (direct to queue)
    cbfg.Send<ProcessPayment>(x =>
    {
        // Direct sends don't go through exchanges
    });

    // Receive endpoints
    cbfg.ReceiveEndpoint("order-service", e =>
    {
        e.PrefetchCount = 64;
        e.ConfigureConsumer<OrderConsumer>(context);
    });

    cbfg.ConfigureEndpoints(context);
});
```

---

## Persistence (EF Core / PostgreSQL)

### Saga State in PostgreSQL

**Configuration**:

```csharp
services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;

            r.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
            {
                builder.UseNpgsql(connectionString, m =>
                {
                    m.MigrationsAssembly(typeof(Program).Assembly.GetName().Name);
                    m.MigrationsHistoryTable("__OrderStateDbContextHistory");
                });
            });

            r.UsePostgres();
        });
});
```

**PostgreSQL Optimistic Concurrency** (using `xmin` system column):

```csharp
public sealed class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public uint RowVersion { get; set; }  // Maps to xmin
}

public sealed class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState).HasMaxLength(64);

        // Use PostgreSQL xmin as row version
        entity.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
```

### Repository Notes: RowVersion in this Project

This repository uses PostgreSQL optimistic concurrency with `xmin` for both saga state and read model projection.

- `OrderState.RowVersion` is mapped to `xmin` (`xid`) in `OrderStateMap`.
- `OrderReadModel.RowVersion` is also mapped to `xmin` (`xid`) in `OrderSagaDbContext`.
- Both mappings use `IsRowVersion()` with a `uint` CLR property.

Why this matters:

- In optimistic mode, MassTransit expects a row version token for safe concurrent updates.
- Mapping `xmin` avoids adding an extra physical rowversion column in PostgreSQL.
- This does not replace endpoint partitioning or idempotency; it complements them.

Migration note:

- Mapping to PostgreSQL `xmin` is usually a model/snapshot concern and may not require DDL for a new column.
- Still generate and review a migration whenever mapping changes, to keep EF metadata in sync.

### Outbox Pattern (Entity Framework)

```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ReceiveEndpoint("submit-order", e =>
        {
            e.UseEntityFrameworkOutbox<OrderDbContext>(context);
            e.ConfigureConsumer<SubmitOrderConsumer>(context);
        });
    });
});
```

Benefits:

- Outbox messages persisted with business data in same transaction
- Messages sent reliably only after consumer completes
- Prevents duplicate publishes on retry

### Bus Outbox vs Endpoint Outbox

There are two different concerns that are easy to mix:

1. `AddEntityFrameworkOutbox<TDbContext>(...)`
   - Registers durable inbox/outbox persistence and delivery services.
   - `UseBusOutbox()` attaches scoped `IPublishEndpoint`/`ISendEndpointProvider` operations to that outbox.

2. `UseEntityFrameworkOutbox<TDbContext>(context)` on a receive endpoint
   - Applies durable inbox/outbox middleware to messages processed by that endpoint.
   - Useful for consumers that must atomically persist state and publish/send follow-up messages.

Important: enabling outbox everywhere is not automatically correct. The endpoint must have a real transactional persistence boundary and the delivery service must fit the runtime topology.

---

## Configuration Patterns

### Explicit Consumer Path (No Reflection)

```csharp
// ❌ Avoid magic discovery
services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(Program).Assembly);  // Reflection-based
});

// ✅ Prefer explicit registration
services.AddMassTransit(x =>
{
    x.AddConsumer<SubmitOrderConsumer>()
        .Endpoint(e => e.Name = "submit-order");

    x.AddConsumer<OrderAcceptedConsumer>()
        .Endpoint(e => e.Name = "order-accepted");

    // Sagas
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.AddDbContext<DbContext, OrderStateDbContext>((_, builder) =>
            {
                builder.UseNpgsql(connectionString);
            });
            r.UsePostgres();
        })
        .Endpoint(e =>
        {
            e.Name = "order-saga";
            e.PrefetchCount = 64;  // Keep default
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

### Extension Methods for Reusability

```csharp
// Enable reuse across services (future componentization)
public static class OrderProcessingMassTransitExtensions
{
    public static IRegistrationConfigurator AddOrderProcessingConsumers(
        this IRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<SubmitOrderConsumer>()
            .Endpoint(e => e.Name = "submit-order");

        return cfg;
    }

    public static IRegistrationConfigurator AddOrderProcessingSagas(
        this IRegistrationConfigurator cfg,
        string connectionString)
    {
        cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                r.AddDbContext<DbContext, OrderStateDbContext>((_, builder) =>
                {
                    builder.UseNpgsql(connectionString);
                });
                r.UsePostgres();
            })
            .Endpoint(e =>
            {
                e.Name = "order-saga";
                e.PrefetchCount = 64;
            });

        return cfg;
    }
}

// Usage:
services.AddMassTransit(x =>
{
    x.AddOrderProcessingConsumers();
    x.AddOrderProcessingSagas(connectionString);

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

---

## Prefetch Rationalization

### RabbitMQ QoS (Quality of Service)

**Default Prefetch**: `64` messages per RabbitMQ specification

**What It Means**:

- Consumer requests 64 messages from queue
- Consumer processes one at a time
- After each success, consumer requests 1 more to maintain 64 in-flight
- On failure (exception), consumer stops requesting until retry completes

**When to Adjust**:

| Scenario                          | Prefetch | Reason                                  |
| --------------------------------- | -------- | --------------------------------------- |
| Default (balanced)                | 64       | MassTransit default                     |
| Database bottleneck               | 16-32    | Limits concurrent DB ops                |
| Memory constrained                | 16-32    | Reduces memory footprint                |
| High-throughput, light processing | 128+     | Maximize throughput (if infra supports) |
| High-latency network              | 128+     | Tolerate network delays                 |

**Important**: Prefetch controls **broker-side buffering**. Use `ConcurrentMessageLimit` for app-side concurrency control.

**Configuration Guidance**:

```csharp
cfg.ReceiveEndpoint("order-queue", e =>
{
    // Keep prefetch at default unless clear bottleneck exists
    e.PrefetchCount = 64;  // MassTransit default

    // Control app-level concurrency separately
    e.ConcurrentMessageLimit = 20;  // Subset of prefetch

    e.ConfigureConsumer<OrderConsumer>(context);
});
```

**Anti-Pattern**:

```csharp
// ❌ Don't lower prefetch just to "go slower"
e.PrefetchCount = 4;  // Hurts throughput unnecessarily

// ✅ Use ConcurrentMessageLimit instead
e.PrefetchCount = 64;
e.ConcurrentMessageLimit = 4;
```

---

## Definitions Catalog (MassTransit)

MassTransit supports explicit configuration objects to remove "configuration in bulk" and make behavior discoverable per endpoint/consumer/saga.

### 1) ConsumerDefinition<TConsumer>

Use when you want endpoint naming, retry, outbox, and concurrency close to each consumer.

```csharp
public sealed class SubmitOrderConsumerDefinition : ConsumerDefinition<SubmitOrderConsumer>
{
    public SubmitOrderConsumerDefinition()
    {
        EndpointName = "submit-order";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SubmitOrderConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(5,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)));
        endpointConfigurator.UseInMemoryOutbox();
    }
}
```

### 2) SagaDefinition<TSaga>

Use when configuring classic saga consumers (`ISaga`) endpoints.

### 3) SagaStateMachineDefinition<TStateMachine, TInstance>

Use when configuring state-machine saga endpoints (`MassTransitStateMachine<T>`). This is the most relevant saga definition for this repository.

```csharp
public sealed class OrderStateDefinition : SagaStateMachineDefinition<OrderState>
{
    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<OrderState> sagaConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Immediate(3));
    }
}
```

### 4) ExecuteActivityDefinition<TActivity, TArguments>

### 5) CompensateActivityDefinition<TActivity, TLog>

Use with Courier/Routing Slips when activities need endpoint-level policies.

### 6) Endpoint definitions via IEndpointDefinition<T>

Use for reusable endpoint naming/settings policies shared across multiple registrations.

---

## Definitions Adoption for This Repository

This repository already uses explicit definitions for worker consumers and the saga endpoint:

- `ProcessPaymentConsumerDefinition`
- `RefundPaymentConsumerDefinition`
- `ReserveInventoryConsumerDefinition`
- `OrderReadModelProjectorConsumerDefinition`
- `OrderStateDefinition`

Topology remains in dedicated configuration modules, while endpoint behavior stays close to each consumer/saga definition.

### Current rule set

1. Keep topology helpers (`ConfigureOrderEventsConsumption`, routing-key helpers, exchange naming) in topology/configuration modules.
2. Keep endpoint behavior (retry, kill switch, concurrency, endpoint name) in `ConsumerDefinition`/`SagaDefinition` types.
3. Use `OrderMessagingTopology.Queues.*` constants for every receive endpoint name.
4. Avoid raw string queue names in code paths that define endpoints.

### Expected benefits

1. Endpoint behavior becomes explicit at consumer/saga type level.
2. Lower risk of accidental drift between endpoints.
3. Easier debugging of binding/queue policy mismatches over time.

### Practical note for EventContext<T>

When using `EventContext<T>` envelopes, definitions do not replace topology requirements:

1. Message topology must be configured for `EventContext<TPayload>` (entity name + exchange type).
2. Bindings must target the same exchange and routing key convention (`source.entity.action`).
3. Consumer message type must exactly match the published envelope type.

---

## Repository Decisions

This section records validated architecture decisions for `mt-saga-order-processing`.

Scope note:

- keep high-level summary text in `README.md`
- keep the full decision log and debugging-derived rationale here in the KB

### Queue Naming Standard

- Canonical queue names live in `OrderMessagingTopology.Queues`
- Pattern: `{domain}.{purpose}-queue`
- Examples:
  - `orders.saga-queue`
  - `orders.process-payment-queue`
  - `orders.refund-payment-queue`
  - `orders.reserve-inventory-queue`
  - `orders.read-model-queue`

Reasoning:

- Keeps queue names aligned across Saga, workers, and tests
- Avoids collision with exchange names in RabbitMQ
- Eliminates drift caused by raw string literals such as `"process-payment"`

### Exchange and Routing Key Standard

- Exchange: `orders.events-exchange`
- Exchange type: `topic`
- Routing key pattern: `{sourceService}.{entity}.{action}`
- Example: `orders.order.payment-processed`

Repository implementation:

- Publish topology is configured for `EventContext<TPayload>`
- Routing keys are generated by `TopicRoutingKeyHelper.GenerateRoutingKey(...)`
- Consumers bind queues to `orders.events-exchange` using the same routing convention

### Command Routing Standard

For saga-to-worker commands, this repository uses explicit queue URIs in the state machine:

```csharp
.Send(new Uri("queue:orders.process-payment-queue"), ...)
.Send(new Uri("queue:orders.reserve-inventory-queue"), ...)
.Send(new Uri("queue:orders.refund-payment-queue"), ...)
```

Why explicit URIs instead of relying only on `EndpointConvention`:

- `EndpointConvention` is static global state
- test processes can race or share convention state unexpectedly
- explicit queue URIs make the routing deterministic in saga execution

`EndpointConvention` is still registered as a fallback and for consistency, but explicit queue URIs are the authoritative route in the saga.

### Worker Registration Pattern

The repository runtime pattern is now intentionally direct:

- worker consumers are registered in each worker `Program.cs`
- registration uses explicit `AddConsumer<TConsumer, TDefinition>()`
- `cfg.ConfigureEndpoints(context)` materializes the RabbitMQ receive endpoints from the consumer definitions

Previously proposed placeholder extension methods were removed because they were unused and created drift between code and docs.

If future componentization requires a reusable worker-registration module, create a real service-specific extension package only when it becomes an active runtime path.

### Outbox Placement Decision Matrix

| Location                                               | Outbox State                                       | Decision                                    | Reason                                                                                                        |
| ------------------------------------------------------ | -------------------------------------------------- | ------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `AddSagaOrchestrationMassTransit`                      | `AddEntityFrameworkOutbox` removed                 | No bus outbox in saga orchestration service | HTTP publishes like `OrderCreated` were buffered without any `SaveChanges`, so messages never left the outbox |
| `OrderStateDefinition.ConfigureSaga`                   | `UseEntityFrameworkOutbox` removed                 | No endpoint outbox on saga receive endpoint | Saga command sends were trapped in the outbox and not delivered in the tested runtime shape                   |
| `AddWorkerMassTransit` / `AddWorkerServiceMassTransit` | `AddEntityFrameworkOutbox` + `UseBusOutbox()` kept | Workers retain durable outbox behavior      | Consumers persist work and publish events in one transactional boundary                                       |
| `OrderReadModelProjectorConsumerDefinition`            | No outbox                                          | Projector stays retry-only                  | Inbox deduplication would suppress event deliveries the projector must process independently                  |

### Current Inbox/Outbox Usage in This Repository

Yes, inbox/outbox is still used, but only where it is architecturally valid.

Active usage:

- PaymentService workers
- InventoryService workers

Worker flow:

```text
Consumer receives command
-> business work executes
-> event is published via IPublishEndpoint / ConsumeContext
-> EF Outbox persists outgoing message with DB transaction
-> MassTransit delivery service flushes outbox to RabbitMQ
```

Non-outbox paths by design:

- OrderService HTTP entry point
- Saga orchestration endpoint
- Read-model projector endpoint

### Producer Interface Decision for This Repository

- HTTP/application event publishing: use `IPublishEndpoint`
- HTTP/application direct command dispatch: use `ISendEndpointProvider`
- Consumer/saga message production: prefer `ConsumeContext`
- `IBus` is not the default application dependency

This was re-aligned after debugging. A temporary use of `IBus` during diagnosis was replaced with `IPublishEndpoint` once the real outbox misconfiguration was fixed.

---

## Known Discoveries

These are major validated discoveries from integration and end-to-end debugging. They must be preserved when changing messaging infrastructure.

### Discovery 1: Bus Outbox in HTTP orchestration path can suppress event publication

Symptom:

- `OrderCreated` was published from HTTP but never consumed downstream.

Root cause:

- `UseBusOutbox()` was active in the saga orchestration service.
- the HTTP endpoint did not commit a DbContext transaction.
- the outgoing publish stayed buffered and was never dispatched.

Resolution:

- remove `AddEntityFrameworkOutbox` from `AddSagaOrchestrationMassTransit`
- use `IPublishEndpoint` in `CreateOrderEndpoint`

### Discovery 2: Endpoint EF outbox on the saga can trap command sends

Symptom:

- Payment and inventory workers did not receive saga commands in integration tests.

Root cause:

- `UseEntityFrameworkOutbox` on the saga endpoint buffered sends such as `ProcessPayment`, `ReserveInventory`, and `RefundPayment`.
- in the tested runtime shape, that delayed dispatch broke orchestration progression.

Resolution:

- remove `UseEntityFrameworkOutbox` from `OrderStateDefinition.ConfigureSaga`
- keep retry, kill switch, and partitioner only

### Discovery 3: The read-model projector must not share inbox deduplication assumptions with the saga

Symptom:

- read-model progression missed status updates under retry/deduplication conditions.

Root cause:

- projector delivery semantics are independent of saga persistence semantics.
- inbox deduplication at the projector endpoint can suppress updates the read model still needs to apply.

Resolution:

- keep the projector endpoint retry-only
- do not apply EF outbox/inbox middleware to `OrderReadModelProjectorConsumerDefinition`

### Discovery 4: Raw queue names create drift and regressions

Symptom:

- refactoring left endpoint names such as `"process-payment"` that no longer matched the canonical queue naming scheme.

Resolution:

- use `OrderMessagingTopology.Queues.*` everywhere endpoints are named
- treat the constants class as the single source of truth for queue names

### Discovery 5: `EndpointConvention` alone is not sufficient as routing authority in tests

Symptom:

- command routing behaved inconsistently across test runs/processes.

Resolution:

- send commands from the saga using explicit queue URIs
- keep idempotent `EndpointConvention` registration only as a secondary mechanism

---

## Documentation URLs by Topic

Use the links below as the source index for each subject in this KB.

| Topic                          | Official URL                                                                                 |
| ------------------------------ | -------------------------------------------------------------------------------------------- |
| Main documentation             | https://masstransit.io/documentation                                                         |
| Consumers                      | https://masstransit.io/documentation/configuration/consumers                                 |
| Consumer definitions           | https://masstransit.io/documentation/configuration/consumers#consumer-definitions            |
| Producers (send/publish)       | https://masstransit.io/documentation/concepts/producers                                      |
| Message topology               | https://masstransit.io/documentation/configuration/topology/message                          |
| RabbitMQ transport             | https://masstransit.io/documentation/transports/rabbitmq                                     |
| Sagas (overview)               | https://masstransit.io/documentation/patterns/saga                                           |
| Saga state machine             | https://masstransit.io/documentation/patterns/saga/state-machine                             |
| Saga persistence (EF)          | https://masstransit.io/documentation/configuration/persistence/entity-framework              |
| Saga state machine definitions | https://masstransit.io/documentation/configuration/sagas/state#saga-state-machine-definition |
| Retry / middleware             | https://masstransit.io/documentation/concepts/exceptions                                     |
| Outbox                         | https://masstransit.io/documentation/patterns/in-memory-outbox                               |
| Transactional Outbox           | https://masstransit.io/documentation/configuration/middleware/outbox                         |
| Routing slips / activities     | https://masstransit.io/documentation/concepts/routing-slips                                  |

---

## Best Practices Summary

### Do's ✅

1. **Use records for messages** (immutable, clean)
2. **Explicit consumer registration** (no assembly scanning)
3. **Sagas for orchestration** (state, compensation, clear flow)
4. **Outbox for reliability** (preventing duplicate publishes)
5. **PostgreSQL optimistic concurrency** (xmin, fast)
6. **Keep prefetch at ~64** (unless bottleneck proven)
7. **Extension methods for DRY** (future componentization)
8. **ConsumeContext over IBus** (enables tracing, correlation)
9. **IActivity<T> for saga activities** (explicit, composable)
10. **IConsumer<T> explicitly** (no magic discovery)
11. **Use queue constants** (`OrderMessagingTopology.Queues.*`) for endpoint names
12. **Document validated discoveries** whenever messaging behavior changes

### Don'ts ❌

1. **Avoid assembly scanning** (AddConsumers, AddSagas)
2. **No message inheritance** (contracts not OOP)
3. **Don't abuse reflection** (prefer explicit config)
4. **Don't lower prefetch arbitrarily** (use ConcurrentMessageLimit)
5. **Don't send from IBus in consumers** (use ConsumeContext)
6. **Don't mix IConsumer implementations** (one per message type)
7. **No business logic in consumers** (keep them thin)
8. **Don't ignore correlation IDs** (break tracing)
9. **Don't enable outbox blindly on every endpoint**
10. **Don't use raw queue name strings when a topology constant exists**

---

## References

- **Official Docs**: https://masstransit.io/documentation
- **GitHub**: https://github.com/MassTransit/MassTransit
- **Patterns**: Saga (Princeton), Enterprise Integration Patterns

---

**Last Updated**: March 25, 2026
**Purpose**: Internal KB for mt-saga-order-processing project
**Maintained By**: Development Team
