using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Swashbuckle.AspNetCore.Annotations;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

public static class GetOrderByIdEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            OrderSagaDbContext dbContext,
            EndpointPipeline<GetOrderByIdQuery, IResult> pipeline,
            CancellationToken ct) =>
        {
            var query = new GetOrderByIdQuery(orderId);

            return await pipeline.ExecuteAsync(query, ct, async () =>
            {
                var order = await dbContext.Orders
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.OrderId == query.OrderId, ct)
                    .ConfigureAwait(false);

                if (order is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(new GetOrderByIdResponse(order.OrderId, order.Status));
            }).ConfigureAwait(false);
        })
        .WithName("GetOrderById")
        .WithTags("Orders")
        .Produces<GetOrderByIdResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithMetadata(new SwaggerOperationAttribute(
            summary: "Get order status by id",
            description: "Returns the order processing status using the order identifier from route."));
    }
}
