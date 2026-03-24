using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.InventoryService.Consumers;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddWorkerMassTransit(
    builder.Configuration,
    typeof(ReserveInventoryConsumer));

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
