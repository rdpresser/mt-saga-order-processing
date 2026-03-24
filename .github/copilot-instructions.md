# Copilot Instructions — mt-saga-order-processing

---

# Project Overview

This project demonstrates a distributed system using **MassTransit Saga (Orchestration pattern)** built with **.NET 10 / C#**.

The system coordinates:

- Payment processing
- Inventory reservation
- Order confirmation

With:

- Compensation flow
- Retry policies
- Idempotency guarantees
- Reliable messaging (Outbox)

---

# Design Goals

This project is intentionally designed to:

- Demonstrate real-world distributed system patterns
- Showcase Saga orchestration using MassTransit
- Apply DDD principles in a pragmatic way (DDD-light)
- Balance clarity vs complexity
- Be easy to run and evaluate

---

# Architectural Decisions

## Monorepo

We use a monorepo with multiple internal projects.

- Simplifies execution
- Improves evaluation experience
- Still models service boundaries

> In production, services would be deployed independently.

## Single Database (PostgreSQL)

We use one PostgreSQL database:

- Reduces setup complexity
- Focuses on Saga behavior
- Keeps the project runnable

> In real-world systems, each service would own its own database.

## Orchestration (Saga) over Choreography

Reasons:

- Centralized control of workflow
- Explicit state transitions
- Easier debugging
- Deterministic compensation logic

---

# Solution Structure

The solution follows monorepo boundaries while keeping all project names with the `MT.Saga.OrderProcessing` prefix.

```text
mt-saga-order-processing/
├── src/
│   ├── MT.Saga.OrderProcessing.Contracts/
│   │   ├── Events/
│   │   └── Commands/
│   ├── MT.Saga.OrderProcessing.Saga/
│   ├── MT.Saga.OrderProcessing.Infrastructure/
│   │   ├── MassTransit/
│   │   └── Persistence/
│   └── Services/
│       ├── MT.Saga.OrderProcessing.OrderService/
│       ├── MT.Saga.OrderProcessing.PaymentService/
│       └── MT.Saga.OrderProcessing.InventoryService/
├── tests/
│   └── MT.Saga.OrderProcessing.Tests/
├── docker-compose.yml
└── README.md
```

---

# Bounded Contexts (DDD Light)

| Context   | Responsibility     |
| --------- | ------------------ |
| Order     | Entry point        |
| Payment   | Payment processing |
| Inventory | Stock reservation  |
| Saga      | Orchestration      |

DDD is applied as:

- Logical separation of responsibilities
- Explicit service boundaries
- Shared contracts and common infrastructure

DDD is NOT applied as:

- Heavy layers (Application/Domain/Infrastructure split inside every microservice)
- Unnecessary abstraction for a showcase project

---

# Contracts

## Events (past tense)

```csharp
public record OrderCreated(Guid OrderId);
public record PaymentProcessed(Guid OrderId);
public record PaymentFailed(Guid OrderId);
public record InventoryReserved(Guid OrderId);
public record InventoryFailed(Guid OrderId);
public record OrderConfirmed(Guid OrderId);
public record OrderCancelled(Guid OrderId);
```

## Commands (imperative)

```csharp
public record ProcessPayment(Guid OrderId);
public record ReserveInventory(Guid OrderId);
public record RefundPayment(Guid OrderId);
public record CancelOrder(Guid OrderId);
```

Rules:

- Use `record` for events and commands
- Keep events immutable and in past tense
- Keep commands imperative

---

# Saga State Machine

## State Diagram

```text
Initial
  ↓
PaymentProcessing
  ↓
InventoryReserving
  ↓
Confirmed
  ↘
   Cancelled (compensation)
```

## Rules

- Always inherit from `MassTransitStateMachine<TState>`
- Always correlate by `OrderId`
- Always use `CorrelationId = OrderId`
- All transitions are event-driven
- Saga is the single source of truth
- Consumers never decide flow; Saga does
- Always call `SetCompletedWhenFinalized()`

Example:

```csharp
Event(() => OrderCreated, e => e.CorrelateById(ctx => ctx.Message.OrderId));
```

---

# Compensation Flow

When `InventoryFailed`:

1. Saga sends `RefundPayment`
2. Saga publishes `OrderCancelled`
3. Saga finalizes

Compensation must always be triggered by the Saga, never by consumers.

---

# Idempotency Rules

- All consumers must be idempotent
- Message delivery is at-least-once
- Duplicate messages must not cause duplicate side effects
- Check for already-processed operations before external actions

---

# Retry and Error Handling

Retries are configured per consumer endpoint, not in the state machine:

```csharp
e.UseMessageRetry(r =>
{
    r.Exponential(5,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(5));
});
```

## Dead Letter

- Failed messages are moved automatically to `_error` queues
- Error queues must be monitored and reprocessed when needed

---

# Outbox Pattern (MassTransit Native)

Use Entity Framework Outbox for reliability:

```csharp
e.UseEntityFrameworkOutbox<SagaDbContext>(context);
```

Benefits:

- Prevents duplicate publishes on retry
- Keeps DB and broker writes consistent
- Improves reliability under failures

---

# Saga Persistence (PostgreSQL)

Use PostgreSQL for saga persistence:

```csharp
options.UseNpgsql(connectionString);
```

Use optimistic concurrency:

```csharp
r.ConcurrencyMode = ConcurrencyMode.Optimistic;
```

This avoids race conditions and supports safe concurrent processing.

---

# Testing

Use `MassTransit.Testing` with `ITestHarness`.

Minimum assertions:

- State transitions
- Event emissions
- Finalization behavior

---

# Mandatory Test Coverage Per Implementation

For every implementation or behavior change in this repository, tests must be created or updated to validate end-to-end reliability of the business flow.

Required test levels for each implementation:

- Unit tests for local logic and edge cases
- Integration tests for service, messaging, persistence, and infrastructure interaction
- End-to-end (E2E) tests for complete workflow validation across the Saga orchestration

Coverage expectation:

- Validate success and failure paths
- Validate compensation behavior when failures occur
- Validate idempotency and retry-safe behavior
- Validate performance and runtime stability with k6 smoke and load tests
- Ensure overall flow behavior is verified at 100% of intended scenarios for the implemented change

Performance test expectation:

- k6 smoke tests are mandatory for quick health and flow validation
- k6 load tests are mandatory to validate behavior under sustained and concurrent traffic
- k6 scenarios must cover critical Saga orchestration paths and compensation paths

---

# Infrastructure

`docker-compose` includes:

- RabbitMQ (management enabled)
- PostgreSQL

---

# Key Principles

## 1. Failures are expected

The system must recover gracefully.

## 2. Eventual consistency

Avoid distributed transactions.

## 3. Idempotency first

Every operation must be retry-safe.

## 4. Simplicity over overengineering

This is a demonstration project, not a production platform.

---

# Cross-Platform Rules

- Do not hardcode path separators; use `Path.Combine()`
- File names must match class names exactly (case-sensitive)
- Prefer LF line endings for scripts and config files

## Maintenance Script Standard

- All maintenance scripts must use PowerShell `.ps1` as the default standard
- Maintenance scripts must run on both Windows and Linux (PowerShell 7+ compatible)
- Use `.sh` scripts only for cases where execution target is inside Docker containers

---

# Reference Code Sources

When implementing or updating code in this repository, always use local reference codebases as implementation guidance and coding standard sources.

Primary dynamic reference source:

- `C:\Projects\mt-saga-order-processing\references`

Rules for this source:

- Treat this folder as dynamic: inspect whatever projects currently exist inside it.
- Do not hardcode assumptions about which projects are present.
- Reuse proven patterns, naming, structure, and coding style from those projects when relevant.
- Prefer conventions that align with Saga orchestration, messaging reliability, and maintainable boundaries.

Secondary reference source:

- `C:\Projects\tc-agro-solutions`

Rules for this source:

- Use it as an additional reference for architecture, conventions, and implementation patterns.
- Apply patterns only when they fit this repository's context and design goals.
- If both sources differ, prioritize consistency with this repository's documented principles first.

---

# Build and Dependency Standards (CPM)

This repository must use Central Package Management (CPM) as the package versioning standard.

Rules:

- Keep `ManagePackageVersionsCentrally=true` in `Directory.Packages.props`
- Do not declare package versions directly inside `.csproj` files
- Define package versions only in `Directory.Packages.props`
- Keep build defaults and enforcement in `Directory.Build.props` and `Directory.Build.targets`

Reference baseline source for these root files:

- `C:\Projects\tc-agro-solutions\services\farm-service`

Use the following files from that source as structural and quality references:

- `.editorconfig`
- `.gitignore`
- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `global.json`

Apply the same organizational principles while keeping package choices and versions aligned to this repository context.

---

# Local Development

```powershell
# Start infrastructure
.\dev.ps1 up

# Run tests
.\dev.ps1 test

# Stop infrastructure
.\dev.ps1 down
```

RabbitMQ Management UI: http://localhost:15672 (guest/guest)
PostgreSQL: localhost:5432

---

# Interview Positioning

Use this concise explanation:

> This project simulates a distributed system using a Saga pattern with MassTransit.
>
> Although implemented as a monorepo for simplicity, each service is logically isolated and can be deployed independently.

Extended explanation:

> This project demonstrates a Saga-based orchestration using MassTransit.
>
> It coordinates multiple services through events and ensures consistency using compensation logic.
>
> I used PostgreSQL for Saga persistence with optimistic concurrency to prevent race conditions.
>
> I implemented retry policies and the Outbox pattern to guarantee reliable messaging.
>
> The system is designed to be idempotent and resilient to failure.

---

# Observability and Telemetry

## Approach

This project includes basic observability using:

- OpenTelemetry
- Structured logging
- CorrelationId propagation
- .NET Aspire (local orchestration and telemetry)

## OpenTelemetry

Used for:

- Distributed tracing
- Service-to-service visibility
- Saga flow tracking

Example:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("MassTransit")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });
```

## Logging

- Structured logging (Serilog optional)
- CorrelationId included in logs
- End-to-end traceability across services

## Aspire Integration

This project can use .NET Aspire to:

- Run services locally
- Provide a built-in dashboard
- Visualize logs and traces
- Simplify orchestration

Benefits:

- Faster debugging
- Better developer experience
- Zero-friction local observability

## Why This Matters

In distributed systems:

- Debugging without tracing is hard
- Logs alone are not enough
- Cross-service correlation is mandatory

## Interview Insight

> I added OpenTelemetry and basic observability because debugging distributed systems without tracing is extremely challenging.
>
> This provides full visibility of Saga execution across services.
