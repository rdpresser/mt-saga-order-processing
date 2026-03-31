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

## Simplicity Boundary (Important)

This repository is intentionally **not** a full DDD implementation with all layered separations.

- Keep the solution simple and study-friendly
- Prefer pragmatic DDD-light boundaries over heavy abstraction
- Do not introduce full layered DDD architecture unless explicitly requested

---

# Agent Routing Guide

Use the custom agents under `.github/agents` to select the best execution mode for each request.

## Agent Routing Strategy

When multiple specialized agents are available, always choose the most appropriate one based on the user's primary intent.

### Agent Selection Rules

- Use **Test and Fix** when:
    - tests are failing
    - there are regressions or runtime test errors
    - the goal is system stabilization with minimal safe changes

- Use **Test Generator & Coverage Analyzer** when:
    - creating a new feature
    - expanding unit/integration/E2E coverage
    - identifying missing scenarios and edge cases

- Use **Test Reviewer & Smell Detector** when:
    - reviewing test quality and maintainability
    - identifying weak assertions, over-mocking, brittleness, and redundancy
    - improving confidence without changing feature behavior

- Use **Architecture Consistency Guardian** when:
    - validating design decisions
    - checking DDD/CQRS/event-driven consistency
    - identifying structural or architectural risks

- Use **Runtime Behavior & Contract Validator** when:
    - comparing expected behavior with real runtime behavior
    - analyzing telemetry and traces
    - validating event contracts and producer/consumer compatibility

### Behavior Rules

- If the request clearly maps to one concern, use the corresponding agent.
- If the request spans multiple concerns, prioritize primary intent first, then optionally apply secondary-agent reasoning.
- Always prefer the most specialized agent over a generic approach.

### Tool Reliability Fallback (Mandatory)

- If agent invocation fails due to tooling/runtime issues (for example: `mgt.clearMarks is not a function`), stop invoking subagents for that request.
- Continue with **manual routing** in the main agent by following this file's routing rules and executing the required analysis/changes directly.
- Do not retry the same failing subagent call more than once in the same request.
- Record in the final response that manual routing fallback was used due to tool failure.

### Ambiguity Rule (Mandatory)

If routing is not clear, ask a direct clarification question before proceeding, for example:

- "Should I prioritize fixing failing tests (Test and Fix) or reviewing test quality (Test Reviewer & Smell Detector)?"
- "Do you want architecture validation (Architecture Consistency Guardian) or runtime contract validation (Runtime Behavior & Contract Validator)?"

## Daily Workflow (Mental Model)

Use this repeatable quality loop:

**IDEA -> TESTS -> CODE -> VALIDATION -> REVIEW -> ARCHITECTURE -> RUNTIME**

Agent mapping:

- **Test Generator & Coverage Analyzer** -> define behavior through tests
- **Developer implementation** -> implement only what tests require
- **Test and Fix** -> stabilize failures and regressions
- **Test Reviewer & Smell Detector** -> harden test quality
- **Architecture Consistency Guardian** -> validate design consistency
- **Runtime Behavior & Contract Validator** -> validate against real execution

This is the default operating model for continuous quality engineering.

## VS Code Usage (Operational Steps)

### Step 1: Generate tests first (TDD start)

Prompt:

```text
/Test Generator & Coverage Analyzer

Feature: <feature name>
Context:
- Domain: <domain>
- Goal: <goal>

Business Rules:
- <rule 1>
- <rule 2>

Edge Cases:
- <edge case 1>
- <edge case 2>

Goal:
- Full coverage (unit, integration, e2e)
- Strong assertions
- Behavior-focused scenarios

Important:
- Do not generate implementation
- Tests should fail initially (TDD)
```

### Step 2: Implement minimally

Implement the smallest amount of code needed to satisfy the tests.

### Step 3: Stabilize failing tests

Prompt:

```text
/Test and Fix

Context:
- Feature: <feature name>
- Test project: MT.Saga.OrderProcessing.Tests.csproj

Problem:
- Tests are failing after implementation

Done criteria:
- All tests passing
- Correct behavior enforced
- No regressions
- No assertion weakening
```

### Step 4: Review test quality

Prompt:

```text
/Test Reviewer & Smell Detector

Context:
- Feature: <feature name>

Focus:
- weak assertions
- over-mocking
- redundancy
- missing scenarios
- flaky risks
```

### Step 5: Validate architecture

Prompt:

```text
/Architecture Consistency Guardian

Context:
- Feature: <feature name>
- Domain: <domain>

Check:
- DDD invariants
- aggregate consistency
- CQRS separation
- event consistency
```

### Step 6: Validate runtime reality (when applicable)

Prompt:

```text
/Runtime Behavior & Contract Validator

Context:
- Feature: <feature name>

Analyze:
- runtime behavior vs tests
- event contracts
- missing real-world scenarios
```

## Quick Routing Table

| Situation | Recommended Agent |
| --- | --- |
| Creating a feature | Test Generator & Coverage Analyzer |
| Failing tests or regressions | Test and Fix |
| Suspicious/weak tests | Test Reviewer & Smell Detector |
| Architecture/design concerns | Architecture Consistency Guardian |
| Production/runtime mismatch | Runtime Behavior & Contract Validator |

## New Feature Starter Kit (Mental Template)

Use this as the default copy/paste feature template.

### 1) Generator prompt

```text
/Test Generator & Coverage Analyzer

Feature: <FEATURE NAME>

Context:
- Domain: <domain>
- Goal: <business goal>
- Actors: <actors>

Business Rules:
- <rule 1>
- <rule 2>
- <rule 3>

Constraints:
- <constraint 1>
- <constraint 2>

Edge Cases:
- <edge case 1>
- <edge case 2>

Goal:
- Full coverage (unit, integration, e2e)
- Focus on behavior, not implementation
- Strong assertions

Important:
- Do not generate implementation
- Tests must fail initially (TDD)
```

### 2) Validation checklist before done

- Happy path is covered
- Failure paths are covered
- Edge cases are covered
- Tests fail when behavior breaks
- Aggregates protect invariants
- Handlers do not contain domain logic
- Events are domain-meaningful
- No mocks are hiding behavior bugs
- No brittle/flaky test patterns remain

### 3) Daily short version

```text
/Test Generator & Coverage Analyzer
Feature: <name>
Goal: full coverage (unit, integration, e2e)
Include edge cases and failure scenarios

/Test and Fix
Fix failing tests for <feature>
Ensure correct behavior and no regressions

/Test Reviewer & Smell Detector
Review test quality for <feature>

/Architecture Consistency Guardian
Validate architecture consistency for <feature>
```

## Common Mistakes to Avoid

- Skipping Generator and writing code without explicit behavior contracts
- Using Test and Fix as a substitute for design thinking
- Ignoring test quality review and accumulating weak tests
- Ignoring architecture review and increasing hidden technical debt
- Running all agents every time when scope does not require it

## Project Simplicity Constraint (Reinforcement)

Do not push this repository into full layered DDD architecture.

- Keep implementation practical and study-focused
- Favor DDD-light boundaries and clear intent
- Improve quality and maintainability without overengineering

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

# Endpoint Style and Validation

## Primary HTTP Approach

For this repository, prefer **Minimal API + screaming architecture (feature-based organization)** for HTTP endpoints.

Reasons:

- Keeps focus on Saga orchestration and distributed workflow
- Avoids unnecessary framework complexity for endpoint concerns
- Makes the codebase intent-revealing: folder names reflect use cases, not layers
- Keeps cognitive load low while preserving maintainability

Use this pattern mainly in `OrderService` (entry-point context). Worker services should remain focused on messaging consumers.

## Screaming Architecture — Feature Folders as Use Cases

Organize by use case, not by technical layer. Each feature folder is a self-contained vertical slice.

```text
src/Services/MT.Saga.OrderProcessing.OrderService/
└── Features/
    └── Orders/
        ├── CreateOrder/
        │   ├── CreateOrderCommand.cs
        │   ├── CreateOrderCommandValidator.cs
        │   ├── CreateOrderResponse.cs
        │   └── CreateOrderEndpoint.cs
        └── GetOrderById/
            ├── GetOrderByIdQuery.cs
            ├── GetOrderByIdResponse.cs
            └── GetOrderByIdEndpoint.cs
```

Rules:

- Each feature folder represents one use case (not a technical role)
- All files for a use case live together: endpoint, command/query, validator, response
- Do not use flat `Endpoints/`, `Contracts/`, or `Validation/` root folders
- Namespaces must reflect the folder path (e.g., `MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder`)

## CQRS-Inspired Naming Conventions

Use CQRS suffixes to communicate intent clearly.

| HTTP Method     | File Suffix  | Example                          |
| --------------- | ------------ | -------------------------------- |
| POST/PUT/DELETE | `Command`    | `CreateOrderCommand.cs`          |
| GET             | `Query`      | `GetOrderByIdQuery.cs`           |
| All responses   | `Response`   | `CreateOrderResponse.cs`         |
| All validators  | `*Validator` | `CreateOrderCommandValidator.cs` |
| All endpoints   | `*Endpoint`  | `CreateOrderEndpoint.cs`         |

Rules:

- Never use the `Dto` suffix — use `Command`, `Query`, or `Response` instead
- Commands and Queries are `sealed record` types
- Response types are `sealed record` types
- Validators are `sealed class` inheriting `AbstractValidator<T>`

### Commands (POST/PUT/DELETE)

```csharp
public sealed record CreateOrderCommand(decimal Amount, string CustomerEmail);
```

### Queries (GET)

```csharp
public sealed record GetOrderByIdQuery(Guid OrderId);
```

### Responses (all verbs)

```csharp
public sealed record CreateOrderResponse(Guid OrderId);
public sealed record GetOrderByIdResponse(Guid OrderId, string Status, decimal Amount);
```

## Validation Approach (No DataAnnotations)

Keep command/query types clean — no DataAnnotation attributes. Use FluentValidation in dedicated validator classes.

Guidelines:

- Commands and Queries must be pure data records with no annotation attributes
- Validators live in the same feature folder as the command/query they validate
- Register validators via DI scanning and invoke inside endpoints
- Return `Results.ValidationProblem(...)` for invalid input

```csharp
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .WithMessage("Customer email is required.")
            .EmailAddress()
            .WithMessage("Customer email must be a valid email address.");
    }
}
```

### Simple Validation Extension (for lightweight endpoints)

For endpoints that do not use the full pipeline, use an `IValidator<T>` extension to keep validation inline and clean:

```csharp
public static class ValidationExtensions
{
    public static async Task<IResult?> ValidateAsync<T>(
        this IValidator<T> validator,
        T model,
        CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (result.IsValid) return null;
        return Results.ValidationProblem(result.ToDictionary());
    }
}
```

Usage in endpoint:

```csharp
var error = await validator.ValidateAsync(command, ct);
if (error is not null) return error;
```

## Lightweight Endpoint Pipeline (MediatR-style, no MediatR)

Use a **lightweight behavior pipeline** for endpoints that require cross-cutting concerns: validation, logging, caching (GET), and cache invalidation (POST/PUT/DELETE). This avoids adding MediatR while keeping full control over the execution flow.

### Pipeline Rule

```text
Logging → Validation → [CachingBehavior | CacheInvalidationBehavior] → Handler
```

### Infrastructure Location

```text
OrderService/
└── Pipeline/
    ├── IEndpointBehavior.cs
    ├── EndpointPipeline.cs
    ├── ValidationBehavior.cs
    ├── LoggingBehavior.cs
    ├── CachingBehavior.cs
    └── CacheInvalidationBehavior.cs
└── Extensions/
    └── ValidationExtensions.cs
```

### Behavior Interface

```csharp
public interface IEndpointBehavior<TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next);
}
```

### Pipeline Executor

```csharp
public sealed class EndpointPipeline<TRequest, TResponse>
{
    private readonly IEnumerable<IEndpointBehavior<TRequest, TResponse>> _behaviors;

    public EndpointPipeline(IEnumerable<IEndpointBehavior<TRequest, TResponse>> behaviors)
        => _behaviors = behaviors;

    public Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct, Func<Task<TResponse>> handler)
    {
        Func<Task<TResponse>> next = handler;
        foreach (var behavior in _behaviors.Reverse())
        {
            var current = next;
            next = () => behavior.Handle(request, ct, current);
        }
        return next();
    }
}
```

### Behaviors

**ValidationBehavior** — runs FluentValidation; throws `ValidationException` on failure (handled by global exception handler):

```csharp
public sealed class ValidationBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    // ...
    public async Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next)
    {
        var failures = _validators
            .Select(v => v.Validate(new ValidationContext<TRequest>(request)))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();
        if (failures.Count > 0) throw new ValidationException(failures);
        return await next();
    }
}
```

**LoggingBehavior** — structured logging before and after handler:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
{
    // Logs "Handling {Request}" before and "Handled {Request}" after
}
```

**CachingBehavior** — for GET queries (via `ICacheService` abstraction over FusionCache):

```csharp
public sealed class CachingBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
{
    // Uses ICacheService with key "{RequestType}:{hashCode}" and tags for later invalidation
}
```

**CacheInvalidationBehavior** — for commands POST/PUT/DELETE (via `ICacheService` abstraction):

```csharp
public sealed class CacheInvalidationBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
{
    // Calls next(), then _cache.RemoveByTagAsync(tag)
}
```

### DI Registration Rule

Register common behaviors as open generics (applied to all endpoints). Register caching/invalidation as closed generics per specific request type:

```csharp
// Common — all endpoints
services.AddScoped(typeof(IEndpointBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IEndpointBehavior<,>), typeof(ValidationBehavior<,>));

// Per command (POST/PUT/DELETE) — cache invalidation
services.AddScoped<IEndpointBehavior<CreateOrderCommand, IResult>,
    CacheInvalidationBehavior<CreateOrderCommand, IResult>>();

// Per query (GET) — caching
// services.AddScoped<IEndpointBehavior<GetOrderByIdQuery, IResult>,
//     CachingBehavior<GetOrderByIdQuery, IResult>>();

// Pipeline executor
services.AddScoped(typeof(EndpointPipeline<,>));

// Shared cache abstraction registration
services.AddOrderProcessingCaching(configuration);
```

### ValidationException Global Handler

Register an `IExceptionHandler` to convert `ValidationException` to `ValidationProblem` HTTP response:

```csharp
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
```

### Endpoint Usage (Command with Pipeline)

```csharp
app.MapPost("/orders", async (
    CreateOrderCommand command,
    EndpointPipeline<CreateOrderCommand, IResult> pipeline,
    IPublishEndpoint publish,
    CancellationToken ct) =>
{
    return await pipeline.ExecuteAsync(command, ct, async () =>
    {
        var orderId = Guid.NewGuid();
        await publish.Publish(new OrderCreated(orderId), ct);
        return Results.Ok(new CreateOrderResponse(orderId));
    });
});
```

### Adoption Rules

- Use the full pipeline when the endpoint needs validation + logging + caching or cache invalidation
- Use `ValidationExtensions` only for simple endpoints without pipeline
- Commands (POST/PUT/DELETE): Logging + Validation + CacheInvalidation
- Queries (GET): Logging + Validation + Caching
- FusionCache is the preferred engine, but always inject `ICacheService` in app layers (never `IFusionCache` directly)
- Keep cache tag constants per feature (for example, `CacheTags.Orders`)
- `ValidationException` is always handled by the global exception handler, never inside endpoints

## Interview Explanation for Endpoint Choices

Use this explanation:

> I chose Minimal APIs to keep the project simple and focused on the distributed workflow.
>
> I organize endpoints using screaming architecture — feature folders represent use cases, not layers.
>
> I use CQRS-inspired naming: Commands for write operations, Queries for reads, and Response for output types.
>
> I keep validation outside commands/queries using FluentValidation, co-located with each use case to avoid model pollution.
>
> For cross-cutting concerns, I implemented a lightweight MediatR-style pipeline without adding MediatR: validation, logging, query caching, and command cache invalidation. Caching uses a shared `ICacheService` abstraction (backed by FusionCache), which keeps the application layer clean and portable.

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

## Workspace Editing Scope

- In this workspace, apply code changes only inside the `mt-saga-order-processing` folder (`path: "."` in the workspace file)
- Never edit files under the sibling workspace folder `../tc-agro-solutions` unless the user explicitly requests that repository
- Never move, rename, or delete files in `../tc-agro-solutions` while working on this repository

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

### Shared Extensions Baseline (tc-agro common)

When building reusable infrastructure in this repository, inspect and reuse patterns from:

- `C:\Projects\tc-agro-solutions\common\src\TC.Agro.SharedKernel\Infrastructure\Caching`
- `C:\Projects\tc-agro-solutions\common\src\TC.Agro.SharedKernel\Infrastructure\Database`
- `C:\Projects\tc-agro-solutions\common\src\TC.Agro.SharedKernel\Infrastructure\MessageBroker`
- `C:\Projects\tc-agro-solutions\common\src\TC.Agro.SharedKernel\Extensions`

Mandatory reuse principles:

- Prefer service abstractions in app/service layers (e.g., `ICacheService`) and keep vendor libraries behind infrastructure adapters
- Bind infrastructure options with `IOptions<T>` from appsettings sections (`Cache`, `Database`, `Messaging`)
- Centralize infrastructure registrations in extension methods (for example `AddOrderProcessingCaching(...)`)
- Keep helper methods cross-platform and dependency-minimal
- Avoid duplicated plumbing in each service; place reusable extensions under infrastructure/shared modules

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

---

# Debugging Configuration

Prefer a unique debug profile of the AppHost in HTTP in Visual Studio to directly open the dashboard and avoid HTTPS certificate issues during local development.
