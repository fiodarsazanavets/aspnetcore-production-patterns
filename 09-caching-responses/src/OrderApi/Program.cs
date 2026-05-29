using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Diagnostics;
using OrderApi.Exceptions;
using OrderApi.Infrastructure;
using OrderApi.Infrastructure.Persistence;
using OrderApi.Services;
using Polly.CircuitBreaker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFaultSwitches, InMemoryFaultSwitches>();
builder.Services.AddScoped<IOrderStore, PersistentOrderStore>();
builder.Services.AddSingleton<IRequestCoalescer, InMemoryRequestCoalescer>();
builder.Services.AddScoped<OrderWorkflow>();
builder.Services.Configure<ShippingOptions>(
    builder.Configuration.GetSection("Shipping"));
builder.Services.Configure<OrderCacheOptions>(
    builder.Configuration.GetSection("OrderCache"));

builder.Services.AddScoped<CachedOrderReader>();
builder.Services.AddScoped<OrderSummaryService>();

builder.Services.AddDbContextFactory<OrdersDbContext>(options =>
{
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Orders"));
});

builder.Services.AddScoped<IIdempotencyStore, PersistentIdempotencyStore>();

builder.Services.AddSingleton<IShippingFallbackProvider, ShippingFallbackProvider>();

builder.Services.AddScoped<IBackgroundJobStore, PersistentBackgroundJobStore>();
builder.Services.AddScoped<IOrderConfirmationSender, PersistentOrderConfirmationSender>();
builder.Services.AddHostedService<OrderConfirmationWorker>();
builder.Services.AddSingleton<LastKnownOrderSnapshot>();

builder.Services
    .AddHttpClient<IShippingGateway, ShippingGateway>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Shipping:BaseUrl"] ?? "https://localhost:7180");

        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddStandardResilienceHandler();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("OrderSummaryOutput", policy =>
    {
        policy.Expire(TimeSpan.FromSeconds(20))
            .SetVaryByRouteValue("orderId")
            .Tag("orders");
    });
});

builder.Services.AddHybridCache();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContextFactory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrdersDbContext>>();

    await using var dbContext =
        await dbContextFactory.CreateDbContextAsync();

    await dbContext.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseOutputCache();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapPost("/orders", async Task<Results<Created<OrderResponse>, 
        Conflict<ProblemDetailsResponse>, BadRequest<ProblemDetailsResponse>, ProblemHttpResult>> (
    CreateOrderRequest request,
    HttpContext httpContext,
    OrderWorkflow workflow,
    CancellationToken cancellationToken) =>
{
    if (request.Lines.Count == 0)
    {
        return TypedResults.BadRequest(new ProblemDetailsResponse("At least one order line is required."));
    }

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return TypedResults.BadRequest(new ProblemDetailsResponse("Provide an Idempotency-Key header for write operations."));
    }

    try
    {
        var result = await workflow.CreateOrderAsync(idempotencyKey, request, cancellationToken);

        return result.Status switch
        {
            OrderWorkflowStatus.Created => TypedResults.Created($"/orders/{result.Order!.OrderId}", result.Order),
            OrderWorkflowStatus.Replayed => TypedResults.Created($"/orders/{result.Order!.OrderId}", result.Order),
            _ => TypedResults.Conflict(new ProblemDetailsResponse("The same idempotency key is already processing a different request."))
        };
    }
    catch (TimeoutException)
    {
        return TypedResults.Problem(
            title: "Shipping dependency timed out.",
            detail: "The order could not be completed because the shipping dependency did not respond within the allowed time.",
            statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (TransientShippingException)
    {
        return TypedResults.Problem(
            title: "Shipping dependency unavailable.",
            detail: "The shipping dependency failed after the allowed retry attempts.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (BrokenCircuitException)
    {
        return TypedResults.Problem(
            title: "Shipping dependency circuit is open.",
            detail: "The shipping dependency is currently protected by an open circuit breaker. Try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/orders/{orderId:guid}", async (
    Guid orderId,
    CachedOrderReader reader,
    CancellationToken cancellationToken) =>
{
    var order = await reader.GetOrderAsync(orderId, cancellationToken);

    return order is null
        ? Results.NotFound()
        : Results.Ok(order);
});

app.MapGet("/orders/{orderId:guid}/summary", async (
    Guid orderId,
    OrderSummaryService summaries,
    CancellationToken cancellationToken) =>
{
    var summary = await summaries.GetSummaryAsync(orderId, cancellationToken);

    return summary is null
        ? Results.NotFound()
        : Results.Ok(summary);
});

app.MapGet("/orders/{orderId:guid}/summary-output", async (
    Guid orderId,
    OrderSummaryService summaries,
    CancellationToken cancellationToken) =>
{
    var summary = await summaries.GetSummaryAsync(orderId, cancellationToken);

    return summary is null
        ? Results.NotFound()
        : Results.Ok(summary);
})
.CacheOutput("OrderSummaryOutput");

app.MapPost("/orders/{orderId:guid}/refresh-cache", async (
    Guid orderId,
    CachedOrderReader reader,
    CancellationToken cancellationToken) =>
{
    await reader.InvalidateAsync(orderId, cancellationToken);

    return Results.NoContent();
});

app.MapPost("/orders/{orderId:guid}/refresh-output-cache", async (
    Guid orderId,
    IOutputCacheStore outputCache,
    CancellationToken cancellationToken) =>
{
    await outputCache.EvictByTagAsync("orders", cancellationToken);

    return Results.NoContent();
});

app.MapPost("/diagnostics/faults", (FaultConfiguration request, IFaultSwitches faults) =>
{
    faults.Configure(request);
    return TypedResults.Ok(faults.Current);
});

app.MapGet("/diagnostics/readiness", (IFaultSwitches faults) =>
{
    var ready = !faults.Current.ForceReadinessFailure;
    return ready ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/diagnostics/background-jobs", async (
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    CancellationToken cancellationToken) =>
{
    await using var dbContext =
        await dbContextFactory.CreateDbContextAsync(cancellationToken);

    var jobs = await dbContext.BackgroundJobs
        .OrderByDescending(x => x.CreatedAt)
        .Take(20)
        .Select(x => new
        {
            x.Id,
            x.JobType,
            x.IdempotencyKey,
            x.State,
            x.AttemptCount,
            x.CreatedAt,
            x.LockedUntil,
            x.CompletedAt,
            x.LastError
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(jobs);
});

app.MapGet("/diagnostics/order-confirmations", async (
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    CancellationToken cancellationToken) =>
{
    await using var dbContext =
        await dbContextFactory.CreateDbContextAsync(cancellationToken);

    var confirmations = await dbContext.OrderConfirmations
        .OrderByDescending(x => x.SentAt)
        .Take(20)
        .ToListAsync(cancellationToken);

    return Results.Ok(confirmations);
});


// Fake external endpoint

app.MapGet("/shipping/quote", (
    IFaultSwitches faults) =>
{
    var current = faults.Current;

    if (current.ShippingDelayMs > 0)
    {
        Thread.Sleep(current.ShippingDelayMs);
    }

    if (faults.ShouldFailShippingTransiently())
    {
        return Results.StatusCode(503);
    }

    if (current.ShippingUnavailable)
    {
        return Results.StatusCode(500);
    }

    return Results.Ok(new
    {
        carrier = "DemoCarrier",
        price = 9.99m
    });
});

app.Run();

public partial class Program;

