using FluentValidation;
using Microsoft.Extensions.Configuration;
using MT.Saga.OrderProcessing.Infrastructure.Caching.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();
        services.AddOrderProcessingCaching(configuration);
        services.AddOrderSagaMassTransit(configuration);

        services.AddScoped(typeof(IEndpointBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IEndpointBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IEndpointBehavior<CreateOrderCommand, IResult>,
            CacheInvalidationBehavior<CreateOrderCommand, IResult>>();
        services.AddScoped<IEndpointBehavior<GetOrderByIdQuery, IResult>,
            CachingBehavior<GetOrderByIdQuery, IResult>>();
        services.AddScoped(typeof(EndpointPipeline<,>));
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
