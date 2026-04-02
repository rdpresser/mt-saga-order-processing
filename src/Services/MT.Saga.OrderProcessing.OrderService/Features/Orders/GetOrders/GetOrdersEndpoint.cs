using Microsoft.EntityFrameworkCore;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Swashbuckle.AspNetCore.Annotations;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;

public static class GetOrdersEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/orders", async (
            int? page,
            int? pageSize,
            OrderSagaDbContext dbContext,
            EndpointPipeline<GetOrdersQuery, IResult> pipeline,
            CancellationToken ct) =>
        {
            var query = new GetOrdersQuery(page ?? 1, pageSize ?? 20);

            return await pipeline.ExecuteAsync(query, ct, async () =>
            {
                var skip = (query.Page - 1) * query.PageSize;

                var orders = await dbContext.Orders
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip(skip)
                    .Take(query.PageSize)
                    .Select(x => new GetOrdersResponse(x.OrderId, x.Status, x.CreatedAt, x.UpdatedAt))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                return Results.Ok(orders);
            }).ConfigureAwait(false);
        })
        .WithName("GetOrders")
        .WithTags("Orders")
        .Produces<IReadOnlyCollection<GetOrdersResponse>>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithMetadata(new SwaggerOperationAttribute(
            summary: "List orders",
            description: "Returns paginated orders from the persisted read model used for operational observability."));
    }
}
