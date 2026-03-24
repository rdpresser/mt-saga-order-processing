using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;

public static class CreateOrderEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/orders", async (
            CreateOrderCommand command,
            EndpointPipeline<CreateOrderCommand, IResult> pipeline,
            IPublishEndpoint publish,
            CancellationToken ct) =>
        {
            return await pipeline.ExecuteAsync(command, ct, async () =>
            {
                var orderId = Guid.NewGuid();

                await publish.Publish(new OrderCreated(orderId), ct).ConfigureAwait(false);

                return Results.Created($"/orders/{orderId}", new CreateOrderResponse(orderId));
            }).ConfigureAwait(false);
        })
        .WithName("CreateOrder")
        .WithTags("Orders");
    }
}
