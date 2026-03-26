# MT Saga Order Processing

[![.NET](https://img.shields.io/badge/.NET-10-blue)]()
[![MassTransit](https://img.shields.io/badge/MassTransit-8.x-purple)]()
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Message%20Broker-orange)]()
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Database-blue)]()
[![Architecture](https://img.shields.io/badge/Architecture-Saga%20Orchestration-green)]()

---

## Overview

This project demonstrates a distributed system using the Saga pattern (Orchestration) with MassTransit.

I designed it using an orchestration approach where the Saga coordinates the workflow between Order, Payment, and Inventory services.

Each step is event-driven, and the Saga ensures consistency using compensation logic when failures occur.

I used PostgreSQL to persist the Saga state with optimistic concurrency to prevent race conditions.

To ensure reliability, I implemented retry policies and the Outbox pattern.

Additionally, I added OpenTelemetry and structured logging to provide full observability of the system.

The goal was to build a system that is resilient, idempotent, and able to recover gracefully from failures.

---

## Monorepo Trade-off

This project simulates a distributed system using a Saga pattern with MassTransit.

Although implemented as a monorepo for simplicity, each service is logically isolated and can be deployed independently.

---

## Key Features

- Saga State Machine (MassTransit)
- Event-driven architecture
- Compensation logic (rollback)
- Retry policies with exponential backoff
- Dead-letter handling
- Idempotent consumers
- EF Outbox on worker services for reliable event publication
- PostgreSQL persistence
- Observability with OpenTelemetry
- Local orchestration via .NET Aspire

---

## Architecture

```text
	     ┌──────────────┐
	     │ Order Service│
	     └──────┬───────┘
		     │ OrderCreated
		     ▼
	     ┌──────────────┐
	     │     Saga     │
	     └──────┬───────┘
		     │
	 ┌──────────┴──────────┐
	 ▼                     ▼
┌──────────────┐     ┌──────────────┐
│ Payment Svc  │     │ InventorySvc │
└──────┬───────┘     └──────┬───────┘
	▼                     ▼
 PaymentProcessed     InventoryReserved
	▼                     ▼
	     ┌──────────────┐
	     │   Completed  │
	     └──────────────┘
```

---

## Solution Structure

```text
src/
├── MT.Saga.OrderProcessing.Contracts/
├── MT.Saga.OrderProcessing.Saga/
├── MT.Saga.OrderProcessing.Infrastructure/
├── Services/
│   ├── MT.Saga.OrderProcessing.OrderService/
│   ├── MT.Saga.OrderProcessing.PaymentService/
│   └── MT.Saga.OrderProcessing.InventoryService/
```

---

## Running Locally

### 1. Start Infrastructure

```bash
docker-compose up -d
```

Services:

- RabbitMQ: http://localhost:15672 (guest/guest)
- PostgreSQL: localhost:5432

---

### 2. Run Services

```bash
dotnet run --project src/Services/MT.Saga.OrderProcessing.OrderService
dotnet run --project src/Services/MT.Saga.OrderProcessing.PaymentService
dotnet run --project src/Services/MT.Saga.OrderProcessing.InventoryService
```

---

### 3. Trigger Flow

Send an `OrderCreated` event via API or test harness.

---

## Workflow

1. OrderCreated
2. ProcessPayment
3. ReserveInventory
4. OrderConfirmed

### Failure Path

- InventoryFailed -> RefundPayment -> OrderCancelled

---

## Messaging Decisions

This section is intentionally short. For the detailed reference, validated discoveries, and MassTransit-specific rationale, use `docs/MASSTRANSIT_KB.md`.

### Outbox Placement

- OrderService HTTP entry point publishes with `IPublishEndpoint` and does not use EF Outbox
- Saga orchestration endpoint does not use `UseEntityFrameworkOutbox(...)`
- PaymentService and InventoryService workers use EF Outbox + Bus Outbox
- Read-model projector is retry-only and does not use inbox/outbox middleware

Reasoning:

- HTTP-originated events must be dispatched immediately
- Saga commands must reach worker queues deterministically
- Worker consumers are the correct transactional boundary for durable outbox behavior
- Projector updates must not be suppressed by inbox deduplication

### Routing Summary

- Queue names are defined in `OrderMessagingTopology.Queues`
- Saga-to-worker commands use explicit queue URIs rather than relying only on `EndpointConvention`
- `EndpointConvention` remains registered as a secondary mechanism, not as the primary routing authority

### Producer Interface Summary

- Events: use `IPublishEndpoint` in application code, or `ConsumeContext.Publish` inside consumers
- Commands: use `ISendEndpointProvider` in application code, or `ConsumeContext.Send` inside consumers
- Avoid `IBus` as the default application dependency

### Where To Read More

- Executive summary: `README.md`
- Detailed messaging decisions and discoveries: `docs/MASSTRANSIT_KB.md`
- Refactoring status: `docs/REFACTORING_STATUS.md`

---

## Observability

This project uses:

- OpenTelemetry (tracing and metrics)
- Structured logging
- CorrelationId tracking
- Aspire dashboard (local)

---

## Design Decisions

- Monorepo for simplicity
- Single PostgreSQL database
- Orchestration over choreography
- DDD-light (focus on boundaries, not heavy layering)

---

## Key Principles

- Failures are expected
- Systems must be retry-safe
- Idempotency is mandatory
- Eventual consistency over distributed transactions

---

## How To Explain This Project

> This project demonstrates a Saga-based orchestration using MassTransit.
>
> It coordinates distributed services and ensures consistency using compensation logic.
>
> It uses PostgreSQL for Saga persistence, Outbox for reliability, and OpenTelemetry for observability.
>
> The system is resilient, idempotent, and designed to handle failure gracefully.

---

## Future Improvements

- Azure Service Bus support
- OpenTelemetry exporter (Jaeger or Grafana)
- CI/CD pipeline
- Multi-database architecture
