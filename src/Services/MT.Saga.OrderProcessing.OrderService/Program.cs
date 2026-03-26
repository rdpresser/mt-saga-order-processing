var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOrderService(builder.Configuration);

var app = builder.Build();

// Only apply migrations if not in test context
// Tests handle migrations separately in fixture setup
if (!app.Environment.IsEnvironment("Test"))
{
    await app.ApplyMigrations<OrderSagaDbContext>().ConfigureAwait(false);
}

app.UseOrderService();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
