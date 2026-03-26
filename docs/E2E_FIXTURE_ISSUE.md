# E2E Fixture Issue - Health Check Timeout

**Date:** March 25, 2026  
**status:** 🔴 INVESTIGATING  
**Impact:** E2E tests timeout at fixture initialization (health check never responds)

---

## Problem

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

## Diagnostics Done

✅ Health endpoints (MapDefaultEndpoints) are always mapped (not Development-only)  
✅ Build succeeds with 0 errors  
✅ Unit tests: 47/47 passing  
✅ Migrations moved out of Program.cs (run once in fixture)  
✅ Program.cs does NOT call RunAsync() when env="Test"  
✅ Testcontainers start successfully (PostgreSQL, RabbitMQ, Redis)

## Likely Root Causes

1. **WebApplicationFactory + MassTransit initialization** - App might be failing silently during dependency injection setup (MassTransit configuration, RabbitMQ connection, etc.)
2. **Testcontainer networking** - Container ports may not be accessible to test app in expected way
3. **Configuration missing** - Test app might be missing required settings (e.g., connection strings not reaching app context)
4. **Exception during startup** - App loading but throwing before health endpoint is responsiveAborting before health check completes

## Next Investigation Steps

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

## Current Workaround

E2E tests are currently not running due to this timeout. Unit tests (47/47) pass successfully.

## Related Files

- `/tests/MT.Saga.OrderProcessing.Tests/E2E/Abstractions/FullSagaE2EFixture.cs` - Fixture setup
- `/src/Services/MT.Saga.OrderProcessing.OrderService/Program.cs` - Testable app configuration
- `/src/MT.Saga.AppHost.Aspire.ServiceDefaults/Extensions.cs` - Health endpoint mapping

---

**Note:** The MassTransit refactoring (6 configuration classes) is complete and unit-tested. This E2E issue is a test infrastructure problem separate from the refactoring work.
