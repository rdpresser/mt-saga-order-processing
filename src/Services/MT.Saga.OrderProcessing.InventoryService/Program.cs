using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using MT.Saga.OrderProcessing.InventoryService.Consumers.Definitions;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddWorkerMassTransit(
    builder.Configuration,
    registerConsumers: x =>
    {
        x.AddConsumer<ReserveInventoryConsumer, ReserveInventoryConsumerDefinition>();
    },
    configureReceiveEndpoints: (cfg, context, _) => cfg.ConfigureEndpoints(context));

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
