using MT.Saga.OrderProcessing.OrderService.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrderService(builder.Configuration);

var app = builder.Build();

app.UseOrderService();

await app.RunAsync().ConfigureAwait(false);

