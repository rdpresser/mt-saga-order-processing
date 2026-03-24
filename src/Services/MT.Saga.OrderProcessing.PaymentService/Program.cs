using MT.Saga.OrderProcessing.PaymentService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
