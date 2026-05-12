# ASP.NET Core 10 Production Patterns — Base Project

This is the starting solution for the course demos. It intentionally begins as a simple order API with visible production risks that can be improved throughout the modules.

## What is included

- `OrderApi`: Minimal API for creating and reading orders.
- `IShippingGateway`: External dependency seam for timeout, retry, circuit-breaker, fallback, and fault-injection demos.
- `IIdempotencyStore`: Starting point for safe write retries.
- `IRequestCoalescer`: Starting point for reducing duplicate concurrent work.
- `/diagnostics/faults`: Endpoint for injecting dependency delay, dependency failure, and readiness failure.
- `/diagnostics/readiness`: Readiness endpoint that can be evolved into production checks.
- `OrderApi.Tests`: First unit test around idempotency behavior.

## Suggested course evolution

### Module 1 — Failure modes
Use the API as-is to map request flow through endpoints, workflow services, stores, and the shipping dependency.

### Module 2 — External dependency resilience
Replace the simple `HttpClient` setup with resilience pipelines: timeouts, retries with jitter, circuit breakers, and fallbacks.

### Module 3 — Idempotency
Move `InMemoryIdempotencyStore` to a persistent store and handle processing records, conflict cases, expiry, and replayed responses.

### Module 4 — Caching and request optimization
Add memory/distributed caching around read operations and expand `IRequestCoalescer` to protect expensive resources.

### Module 5 — Stateless horizontal scale
Replace in-memory state with shared state, remove singleton assumptions, and make background work multi-instance safe.

### Module 6 — Diagnosis and readiness
Add controlled failure scenarios, verification tests, and a production readiness checklist.

## Running locally

```bash
dotnet restore
dotnet run --project src/OrderApi
```

Open Swagger from the application root URL or use `src/OrderApi/OrderApi.http`.

> Note: The project targets `net10.0` and assumes the .NET 10 SDK is installed.
