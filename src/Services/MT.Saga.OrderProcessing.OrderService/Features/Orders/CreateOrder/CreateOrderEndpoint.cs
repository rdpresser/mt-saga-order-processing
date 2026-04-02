using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;

public static class CreateOrderEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/orders", async (
            CreateOrderCommand command,
            EndpointPipeline<CreateOrderCommand, IResult> pipeline,
            IPublishEndpoint publishEndpoint,
            IMessagingResilienceOptionsProvider resilienceOptionsProvider,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await pipeline.ExecuteAsync(command, ct, async () =>
            {
                var orderId = Guid.NewGuid();
                var logger = loggerFactory.CreateLogger("OrderPublish");

                var correlationId = ResolveCorrelationId(httpContext);
                var userId = ResolveUserId(httpContext.User);
                var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
                var metadata = BuildHttpMetadata(httpContext);

                var eventContext = EventContext.Create(
                    sourceService: OrderMessagingTopology.SourceService,
                    entity: OrderMessagingTopology.EntityName,
                    action: OrderMessagingTopology.Actions.Created,
                    payload: new OrderCreated(orderId),
                    correlationId: correlationId,
                    causationId: null,
                    userId: userId,
                    isAuthenticated: isAuthenticated,
                    metadata: metadata);

                await publishEndpoint
                    .PublishEventContextWithRetryAsync(eventContext, logger, resilienceOptionsProvider.Current, ct)
                    .ConfigureAwait(false);

                return Results.Created($"/orders/{orderId}", new CreateOrderResponse(orderId));
            }).ConfigureAwait(false);
        })
        .WithName("CreateOrder")
        .WithTags("Orders")
        .Accepts<CreateOrderCommand>("application/json")
        .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithMetadata(new SwaggerOperationAttribute(
            summary: "Create a new order",
            description: "Starts the Saga orchestration by publishing an OrderCreated event in EventContext envelope."));
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("x-correlation-id", out var headerCorrelation)
            && !string.IsNullOrWhiteSpace(headerCorrelation))
        {
            return headerCorrelation.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private static string? ResolveUserId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("user_id");
    }

    private static IDictionary<string, object> BuildHttpMetadata(HttpContext httpContext)
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["http-method"] = httpContext.Request.Method,
            ["http-path"] = httpContext.Request.Path.ToString(),
            ["trace-identifier"] = httpContext.TraceIdentifier,
            ["remote-ip"] = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            ["user-agent"] = httpContext.Request.Headers.UserAgent.ToString()
        };
    }
}
