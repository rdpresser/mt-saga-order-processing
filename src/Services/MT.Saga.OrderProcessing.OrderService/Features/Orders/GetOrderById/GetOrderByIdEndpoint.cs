using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

public static class GetOrderByIdEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            EndpointPipeline<GetOrderByIdQuery, IResult> pipeline,
            CancellationToken ct) =>
        {
            var query = new GetOrderByIdQuery(orderId);

            return await pipeline.ExecuteAsync(query, ct, () =>
                Task.FromResult<IResult>(Results.Ok(new GetOrderByIdResponse(query.OrderId, "Processing")))).ConfigureAwait(false);
        })
        .WithName("GetOrderById")
        .WithTags("Orders");
    }
}
