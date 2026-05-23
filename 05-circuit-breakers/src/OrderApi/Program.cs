using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Diagnostics;
using OrderApi.Exceptions;
using OrderApi.Infrastructure;
using OrderApi.Services;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFaultSwitches, InMemoryFaultSwitches>();
builder.Services.AddSingleton<IOrderStore, InMemoryOrderStore>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<IRequestCoalescer, InMemoryRequestCoalescer>();
builder.Services.AddScoped<OrderWorkflow>();
builder.Services.Configure<ShippingOptions>(
    builder.Configuration.GetSection("Shipping"));

builder.Services.AddSingleton<IShippingFallbackProvider, ShippingFallbackProvider>();

builder.Services
    .AddHttpClient<IShippingGateway, ShippingGateway>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Shipping:BaseUrl"] ?? "https://localhost:5009");
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddPolicyHandler((services, _) =>
    {
        var options =
            services.GetRequiredService<IOptions<ShippingOptions>>().Value;

        var logger =
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("PollyRetry");

        var delays = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay:
                TimeSpan.FromMilliseconds(options.BaseRetryDelayMilliseconds),
            retryCount: options.MaxRetryAttempts - 1);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response =>
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                delays,
                onRetry: (outcome, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        outcome.Exception,
                        "Retry attempt {Attempt} after {DelayMilliseconds} ms. StatusCode: {StatusCode}",
                        attempt,
                        delay.TotalMilliseconds,
                        outcome.Result?.StatusCode);
                });
    })
    .AddPolicyHandler((services, _) => GetCircuitBreakerPolicy(services));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(IServiceProvider services)
{
    var options = services.GetRequiredService<IOptions<ShippingOptions>>().Value;

    var logger = services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("PollyCircuitBreaker");

    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: options.CircuitBreakAfterFailures,
            durationOfBreak: TimeSpan.FromSeconds(options.CircuitBreakDurationSeconds),
            onBreak: (outcome, breakDelay) =>
            {
                logger.LogWarning(
                    outcome.Exception,
                    "Shipping circuit opened for {BreakDelaySeconds} seconds.",
                    breakDelay.TotalSeconds);
            },
            onReset: () =>
            {
                logger.LogInformation("Shipping circuit reset.");
            },
            onHalfOpen: () =>
            {
                logger.LogInformation("Shipping circuit is half-open. The next call is a probe.");
            });
}

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.MapGet("/orders/{orderId:guid}", async Task<Results<Ok<OrderResponse>, NotFound>> (
    Guid orderId,
    IOrderStore store,
    IRequestCoalescer coalescer,
    CancellationToken cancellationToken) =>
{
    var order = await coalescer.RunOnceAsync($"order:{orderId}", () => store.FindAsync(orderId, cancellationToken));
    return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
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

