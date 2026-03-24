using MT.Saga.OrderProcessing.OrderService.Extensions;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Infrastructure.Persistence.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOrderService(builder.Configuration);

var app = builder.Build();

await app.ApplyMigrations<OrderSagaDbContext>().ConfigureAwait(false);

app.UseOrderService();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

