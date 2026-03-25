# MassTransit Knowledge Base (KB) - Internal Reference

**Source**: [Official MassTransit Documentation](https://masstransit.io/documentation)  
**Version**: MassTransit v8.x+  
**Purpose**: Comprehensive internal reference for explicit configuration patterns and best practices.

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

| Scenario | Prefetch | Reason |
|----------|----------|--------|
| Default (balanced) | 64 | MassTransit default |
| Database bottleneck | 16-32 | Limits concurrent DB ops |
| Memory constrained | 16-32 | Reduces memory footprint |
| High-throughput, light processing | 128+ | Maximize throughput (if infra supports) |
| High-latency network | 128+ | Tolerate network delays |

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

### Don'ts ❌

1. **Avoid assembly scanning** (AddConsumers, AddSagas)
2. **No message inheritance** (contracts not OOP)
3. **Don't abuse reflection** (prefer explicit config)
4. **Don't lower prefetch arbitrarily** (use ConcurrentMessageLimit)
5. **Don't send from IBus in consumers** (use ConsumeContext)
6. **Don't mix IConsumer implementations** (one per message type)
7. **No business logic in consumers** (keep them thin)
8. **Don't ignore correlation IDs** (break tracing)

---

## References

- **Official Docs**: https://masstransit.io/documentation
- **GitHub**: https://github.com/MassTransit/MassTransit
- **Patterns**: Saga (Princeton), Enterprise Integration Patterns

---

**Last Updated**: March 2026  
**Purpose**: Internal KB for mt-saga-order-processing project  
**Maintained By**: Development Team
