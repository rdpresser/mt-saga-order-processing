using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using MT.Saga.OrderProcessing.PaymentService.Consumers.Definitions;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
// ConsumerDefinitions declare endpoint name, retry, kill switch, and EF outbox.
// AddWorkerServiceMassTransit registers consumers and calls ConfigureEndpoints automatically.
builder.Services.AddWorkerServiceMassTransit(builder.Configuration, x =>
{
    x.AddConsumer<ProcessPaymentConsumer, ProcessPaymentConsumerDefinition>();
    x.AddConsumer<RefundPaymentConsumer, RefundPaymentConsumerDefinition>();
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
