using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.PaymentService.Consumers;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddWorkerMassTransit(
    builder.Configuration,
    typeof(ProcessPaymentConsumer),
    typeof(RefundPaymentConsumer));

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
