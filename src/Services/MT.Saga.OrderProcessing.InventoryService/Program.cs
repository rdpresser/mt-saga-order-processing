using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.InventoryService.Consumers;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddWorkerMassTransit(
    builder.Configuration,
    registerConsumers: x =>
    {
        x.AddConsumer<ReserveInventoryConsumer>();
    },
    configureReceiveEndpoints: (cfg, context, configuration) =>
    {
        cfg.ReceiveEndpoint("reserve-inventory", endpoint =>
        {
            endpoint.ConfigureCommonReceiveEndpointPolicies(context, configuration);
            endpoint.ConfigureConsumer<ReserveInventoryConsumer>(context);
        });
    });

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
