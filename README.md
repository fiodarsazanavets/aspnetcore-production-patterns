# ASP.NET Core 10 Production Patterns

This project demonstrates a graduate evolution of a realistic ASP.NET Core application by gradually hardening it to make it production-ready. It intentionally begins as a simple order API with visible production risks that can be improved throughout the modules.

## What is included

- `OrderApi`: Minimal API for creating and reading orders.
- `IShippingGateway`: External dependency seam for timeout, retry, circuit-breaker, fallback, and fault-injection demos.
- `IIdempotencyStore`: Starting point for safe write retries.
- `IRequestCoalescer`: Starting point for reducing duplicate concurrent work.
- `/diagnostics/faults`: Endpoint for injecting dependency delay, dependency failure, and readiness failure.
- `/diagnostics/readiness`: Readiness endpoint that can be evolved into production checks.
- `OrderApi.Tests`: First unit test around idempotency behavior.

## Running locally

```bash
dotnet restore
dotnet run --project src/OrderApi
```

Open Swagger from the application root URL or use `src/OrderApi/OrderApi.http`.

> Note: The project targets `net10.0` and assumes the .NET 10 SDK is installed.
