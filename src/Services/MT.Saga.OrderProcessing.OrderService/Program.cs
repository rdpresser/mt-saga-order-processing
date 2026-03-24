using MT.Saga.OrderProcessing.OrderService.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOrderService(builder.Configuration);

var app = builder.Build();

app.UseOrderService();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

