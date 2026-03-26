using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using MT.Saga.OrderProcessing.PaymentService.Consumers.Definitions;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddWorkerMassTransit(
    builder.Configuration,
    registerConsumers: x =>
    {
        x.AddConsumer<ProcessPaymentConsumer, ProcessPaymentConsumerDefinition>();
        x.AddConsumer<RefundPaymentConsumer, RefundPaymentConsumerDefinition>();
    },
    // ConsumerDefinitions carry the endpoint name, retry, kill switch, and EF outbox.
    // ConfigureEndpoints creates the RabbitMQ endpoint from the definition automatically.
    configureReceiveEndpoints: (cfg, context, _) => cfg.ConfigureEndpoints(context));

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
