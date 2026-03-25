using System.Net.Http.Json;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using MT.Saga.OrderProcessing.OrderService.Extensions;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using OrderServiceEntryPoint = MT.Saga.OrderProcessing.OrderService.OrderServiceEntryPoint;

namespace MT.Saga.OrderProcessing.Tests.E2E.Abstractions;

public sealed class FullSagaE2EFixture : IAsyncLifetime
{
    private const string DatabaseName = "mt_saga_order_processing_e2e";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:4.2.3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:8.4.0-alpine")
        .Build();

    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase);

    private IHost? _paymentWorkerHost;
    private IHost? _inventoryWorkerHost;

    public WebApplicationFactory<OrderServiceEntryPoint> OrderServiceWebApplicationFactory { get; private set; } = default!;
    public HttpClient OrderClient { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        await Task.WhenAll(
            _postgresContainer.StartAsync(ct),
            _rabbitMqContainer.StartAsync(ct),
            _redisContainer.StartAsync(ct));

        BuildSettings();

        OrderServiceWebApplicationFactory = new OrderServiceFactory(_settings);
        OrderClient = OrderServiceWebApplicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        OrderClient.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        _paymentWorkerHost = await StartPaymentWorkerAsync(ct);
        _inventoryWorkerHost = await StartInventoryWorkerAsync(ct);

        await WaitForOrderServiceReadinessAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        OrderClient?.Dispose();
        if (OrderServiceWebApplicationFactory is not null)
        {
            await OrderServiceWebApplicationFactory.DisposeAsync();
        }

        if (_inventoryWorkerHost is not null)
        {
            await _inventoryWorkerHost.StopAsync(ct);
            _inventoryWorkerHost.Dispose();
        }

        if (_paymentWorkerHost is not null)
        {
            await _paymentWorkerHost.StopAsync(ct);
            _paymentWorkerHost.Dispose();
        }

        await _redisContainer.DisposeAsync().AsTask();
        await _rabbitMqContainer.DisposeAsync().AsTask();
        await _postgresContainer.DisposeAsync().AsTask();
    }

    public async Task<Guid> CreateOrderAsync(decimal amount, string customerEmail, CancellationToken cancellationToken)
    {
        var response = await OrderClient.PostAsJsonAsync(
            "/orders",
            new CreateOrderCommand(amount, customerEmail),
            cancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken: cancellationToken);
        payload.ShouldNotBeNull();

        return payload!.OrderId;
    }

    public async Task StopInventoryWorkerAsync(CancellationToken cancellationToken)
    {
        if (_inventoryWorkerHost is null)
        {
            return;
        }

        await _inventoryWorkerHost.StopAsync(cancellationToken);
        _inventoryWorkerHost.Dispose();
        _inventoryWorkerHost = null;
    }

    public async Task EnsureInventoryWorkerStartedAsync(CancellationToken cancellationToken)
    {
        if (_inventoryWorkerHost is not null)
        {
            return;
        }

        _inventoryWorkerHost = await StartInventoryWorkerAsync(cancellationToken);
    }

    public async Task PublishInventoryFailedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var hostUri = new Uri($"rabbitmq://{GetRequiredSetting("Messaging:RabbitMq:Host")}:{GetRequiredSetting("Messaging:RabbitMq:Port")}");

        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(hostUri, h =>
            {
                h.Username(GetRequiredSetting("Messaging:RabbitMq:UserName"));
                h.Password(GetRequiredSetting("Messaging:RabbitMq:Password"));
            });

            cfg.Message<EventContext<InventoryFailed>>(x => x.SetEntityName(OrderMessagingTopology.ExchangeName));
            cfg.Publish<EventContext<InventoryFailed>>(x => x.ExchangeType = "topic");
        });

        await bus.StartAsync(cancellationToken);
        try
        {
            var @event = EventContext.Create(
                sourceService: OrderMessagingTopology.SourceService,
                entity: OrderMessagingTopology.EntityName,
                action: OrderMessagingTopology.Actions.InventoryFailed,
                payload: new InventoryFailed(orderId));

            await bus.Publish(@event, context =>
            {
                if (context is RabbitMqSendContext rabbitMqSendContext)
                {
                    rabbitMqSendContext.RoutingKey = TopicRoutingKeyHelper.GenerateRoutingKey(
                        @event.SourceService,
                        @event.Entity,
                        @event.Action);
                }
            }, cancellationToken);
        }
        finally
        {
            await bus.StopAsync(cancellationToken);
        }
    }

    public async Task<bool> WaitForOrderStateAsync(Guid orderId, string expectedState, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            await using var connection = new NpgsqlConnection(BuildDatabaseConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"CurrentState\" FROM \"OrderState\" WHERE \"CorrelationId\" = @orderId";
            command.Parameters.AddWithValue("orderId", orderId);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var currentState = result?.ToString();

            if (string.Equals(currentState, expectedState, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    public async Task<bool> WaitForSagaFinalizedAsync(Guid orderId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            await using var connection = new NpgsqlConnection(BuildDatabaseConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM \"OrderState\" WHERE \"CorrelationId\" = @orderId";
            command.Parameters.AddWithValue("orderId", orderId);

            var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
            if (count == 0)
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    public async Task<bool> WaitForOutboxBodyContainsAsync(Guid orderId, string expectedFragment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var orderIdText = orderId.ToString();

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            await using var connection = new NpgsqlConnection(BuildDatabaseConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM ""OutboxMessage""
WHERE ""Body"" ILIKE @orderIdMatch
    AND ""Body"" ILIKE @fragmentMatch";
            command.Parameters.AddWithValue("orderIdMatch", $"%{orderIdText}%");
            command.Parameters.AddWithValue("fragmentMatch", $"%{expectedFragment}%");

            var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
            if (count > 0)
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    private async Task<IHost> StartPaymentWorkerAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });

        builder.Configuration.AddInMemoryCollection(_settings);
        builder.AddServiceDefaults();
        builder.Services.AddWorkerMassTransit(
            builder.Configuration,
            typeof(ProcessPaymentConsumer),
            typeof(RefundPaymentConsumer));

        var host = builder.Build();
        await host.StartAsync(cancellationToken);

        return host;
    }

    private async Task<IHost> StartInventoryWorkerAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });

        builder.Configuration.AddInMemoryCollection(_settings);
        builder.AddServiceDefaults();
        builder.Services.AddWorkerMassTransit(
            builder.Configuration,
            typeof(ReserveInventoryConsumer));

        var host = builder.Build();
        await host.StartAsync(cancellationToken);

        return host;
    }

    private async Task WaitForOrderServiceReadinessAsync(CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(60);  // Increased from 30 to 60 seconds
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            try
            {
                var response = await OrderClient.GetAsync("/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                
                Console.WriteLine($"Health check returned: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                // Ignore transient startup errors while app initializes.
                Console.WriteLine($"Health check error: {ex.GetType().Name} - {ex.Message}");
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new InvalidOperationException("Order Service did not become healthy within the expected timeout.");
    }

    private void BuildSettings()
    {
        _settings["Database:Postgres:Host"] = _postgresContainer.Hostname;
        _settings["Database:Postgres:Port"] = _postgresContainer.GetMappedPublicPort(5432).ToString();
        _settings["Database:Postgres:Database"] = DatabaseName;
        _settings["Database:Postgres:MaintenanceDatabase"] = "postgres";
        _settings["Database:Postgres:UserName"] = "postgres";
        _settings["Database:Postgres:Password"] = "postgres";

        _settings["Messaging:RabbitMq:Host"] = _rabbitMqContainer.Hostname;
        _settings["Messaging:RabbitMq:Port"] = _rabbitMqContainer.GetMappedPublicPort(5672).ToString();
        _settings["Messaging:RabbitMq:UserName"] = "guest";
        _settings["Messaging:RabbitMq:Password"] = "guest";
        _settings["Messaging:RabbitMq:VirtualHost"] = "/";

        _settings["Cache:Redis:Host"] = _redisContainer.Hostname;
        _settings["Cache:Redis:Port"] = _redisContainer.GetMappedPublicPort(6379).ToString();
        _settings["Cache:Redis:Password"] = string.Empty;
        _settings["Cache:Redis:Secure"] = "false";
        _settings["Cache:Redis:InstanceName"] = "mt-saga-order-processing-e2e";

        _settings["ASPNETCORE_ENVIRONMENT"] = Environments.Development;
    }

    private string BuildDatabaseConnectionString()
    {
        var host = GetRequiredSetting("Database:Postgres:Host");
        var port = GetRequiredSetting("Database:Postgres:Port");

        return $"Host={host};Port={port};Database={DatabaseName};Username=postgres;Password=postgres;SearchPath=public";
    }

    private string GetRequiredSetting(string key)
    {
        return _settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Required setting '{key}' is missing.");
    }

    private sealed class OrderServiceFactory(Dictionary<string, string?> settings)
        : WebApplicationFactory<OrderServiceEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(settings);
            });
        }
    }
}
