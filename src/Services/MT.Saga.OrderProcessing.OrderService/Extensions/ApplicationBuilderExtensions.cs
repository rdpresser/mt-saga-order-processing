using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

namespace MT.Saga.OrderProcessing.OrderService.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseOrderService(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();

        CreateOrderEndpoint.Map(app);
        GetOrderByIdEndpoint.Map(app);

        return app;
    }
}
