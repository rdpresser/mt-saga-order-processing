var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOrderService(builder.Configuration);

var app = builder.Build();

await app.ApplyMigrations<OrderSagaDbContext>().ConfigureAwait(false);

app.UseOrderService();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
