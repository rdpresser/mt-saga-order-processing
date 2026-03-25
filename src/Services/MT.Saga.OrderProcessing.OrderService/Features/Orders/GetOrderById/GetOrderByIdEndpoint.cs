using Microsoft.AspNetCore.Http;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Swashbuckle.AspNetCore.Annotations;

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
        .WithTags("Orders")
        .Produces<GetOrderByIdResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithMetadata(new SwaggerOperationAttribute(
            summary: "Get order status by id",
            description: "Returns the order processing status using the order identifier from route."));
    }
}
