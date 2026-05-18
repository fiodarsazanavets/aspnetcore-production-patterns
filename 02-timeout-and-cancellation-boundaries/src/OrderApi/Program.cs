using Microsoft.AspNetCore.Http.HttpResults;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Diagnostics;
using OrderApi.Infrastructure;
using OrderApi.Services;

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

builder.Services.AddHttpClient<IShippingGateway, ShippingGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Shipping:BaseUrl"] ?? "https://localhost:5009");
    client.Timeout = Timeout.InfiniteTimeSpan;
});

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

app.Run();

public partial class Program;
