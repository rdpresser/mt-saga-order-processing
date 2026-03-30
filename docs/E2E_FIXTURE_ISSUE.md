# E2E Fixture Issue - Health Check Timeout (Historical)

**Date:** March 25, 2026  
**Status:** ✅ RESOLVED (historical record)  
**Impact at the time:** E2E tests timed out at fixture initialization (health check did not respond)

---

## Problem (historical)

When running E2E tests via WebApplicationFactory, the health check endpoint times out after 60+ seconds:

```
Order Service did not become healthy within the expected timeout.
WaitForOrderServiceReadinessAsync() → OrderClient.GetAsync("/health") → No response
```

## Environment

- WebApplicationFactory creates OrderService test app
- Testcontainers: PostgreSQL, RabbitMQ, Redis all starting correctly
- Settings passed to factory via AddInMemoryCollection()
- Factory environment set to "Test" to skip Program.cs migrations
- Health endpoint mapped unconditionally in MapDefaultEndpoints()

## Diagnostics Done (historical)

✅ Health endpoints (MapDefaultEndpoints) are always mapped (not Development-only)  
✅ Build succeeds with 0 errors  
✅ Unit tests at that point: 47/47 passing  
✅ Migrations moved out of Program.cs (run once in fixture)  
✅ Program.cs does NOT call RunAsync() when env="Test"  
✅ Testcontainers start successfully (PostgreSQL, RabbitMQ, Redis)

## Root Cause Summary

This timeout symptom was part of a broader messaging integration mismatch that was fixed later by:

1. validating explicit saga-to-worker command routing with queue URIs,
2. adjusting outbox placement for the tested runtime shape,
3. keeping health endpoints mapped in test environment.

## Investigation Steps (kept for reference)

Would require:

1. Add detailed logging to fixture setup:

   ```csharp
   // Log each stage of fixture initialization
   // Try/catch the health check attempts with better diagnostics
   ```

2. Verify app actually starts:

   ```csharp
   // Create simple GET endpoint that doesn't depend on MassTransit
   // Test if ANY endpoint responds
   ```

3. Check MassTransit/RabbitMQ initialization:

   ```csharp
   // Verify RabbitMQ connectivity from test app
   // Check if MassTransit bus is throwing during configuration
   ```

4. Test without MassTransit:
   ```csharp
   // Create minimal test to see if WebApplicationFactory works without Saga/consumers
   ```

## Current Status

This workaround is no longer active.

The full suite currently passes in the repository runtime shape.
Use `docs/REFACTORING_STATUS.md` as the authoritative status source.

## Related Files

- `/tests/MT.Saga.OrderProcessing.Tests/E2E/Abstractions/FullSagaE2EFixture.cs` - Fixture setup
- `/src/Services/MT.Saga.OrderProcessing.OrderService/Program.cs` - Testable app configuration
- `/src/MT.Saga.AppHost.Aspire.ServiceDefaults/Extensions.cs` - Health endpoint mapping

---

**Note:** Keep this file as historical debugging context only.
