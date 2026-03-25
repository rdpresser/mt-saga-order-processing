using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.PaymentService.Consumers;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddWorkerMassTransit(
    builder.Configuration,
    registerConsumers: x =>
    {
        x.AddConsumer<ProcessPaymentConsumer>();
        x.AddConsumer<RefundPaymentConsumer>();
    },
    configureReceiveEndpoints: (cfg, context, configuration) =>
    {
        cfg.ReceiveEndpoint("process-payment", endpoint =>
        {
            endpoint.ConfigureCommonReceiveEndpointPolicies(context, configuration);
            endpoint.ConfigureConsumer<ProcessPaymentConsumer>(context);
        });

        cfg.ReceiveEndpoint("refund-payment", endpoint =>
        {
            endpoint.ConfigureCommonReceiveEndpointPolicies(context, configuration);
            endpoint.ConfigureConsumer<RefundPaymentConsumer>(context);
        });
    });

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
