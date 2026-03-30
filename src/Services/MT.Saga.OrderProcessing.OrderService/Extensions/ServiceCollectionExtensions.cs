using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;
using System.Reflection;
using MT.Saga.OrderProcessing.Infrastructure.Caching.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MT.Saga Order Service API",
                Version = "v1",
                Description = "Order entry-point API for the Saga orchestration flow."
            });

            options.EnableAnnotations();
            options.SupportNonNullableReferenceTypes();
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();
        services.AddOrderProcessingCaching(configuration);
        services.AddSagaOrchestrationMassTransit(configuration);

        services.AddScoped(typeof(IEndpointBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IEndpointBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IEndpointBehavior<CreateOrderCommand, IResult>,
            CacheInvalidationBehavior<CreateOrderCommand, IResult>>();
        services.AddScoped<IEndpointBehavior<GetOrdersQuery, IResult>,
            CachingBehavior<GetOrdersQuery, IResult>>();
        services.AddScoped<IEndpointBehavior<GetOrderByIdQuery, IResult>,
            CachingBehavior<GetOrderByIdQuery, IResult>>();
        services.AddScoped(typeof(EndpointPipeline<,>));
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
