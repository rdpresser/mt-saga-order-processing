using System.Net;
using System.Net.Http.Json;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MT.Saga.OrderProcessing.Contracts;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using MT.Saga.OrderProcessing.InventoryService.Consumers.Definitions;
using MT.Saga.OrderProcessing.OrderService.Extensions;
using MT.Saga.OrderProcessing.PaymentService.Consumers.Definitions;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;
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
    private static readonly TimeSpan WorkerStartupStabilizationDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WorkerReadinessProbeTimeout = TimeSpan.FromSeconds(90);

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

        // Apply migrations ONCE, before creating web app instances
        await ApplyMigrationsOnceAsync(ct);

        OrderServiceWebApplicationFactory = new OrderServiceFactory(_settings);
        OrderClient = OrderServiceWebApplicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        OrderClient.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        _paymentWorkerHost = await StartPaymentWorkerAsync(ct);
        _inventoryWorkerHost = await StartInventoryWorkerAsync(ct);

        await WaitForOrderServiceReadinessAsync(ct);
        await EnsureHappyPathWorkersReadyAsync(ct);
    }

    private async Task ApplyMigrationsOnceAsync(CancellationToken ct)
    {
        // Create a temporary DbContext just for running migrations
        // Use the connection string built from testcontainer settings
        var connectionString = BuildDatabaseConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<OrderSagaDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var context = new OrderSagaDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync(ct);
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
        if (_inventoryWorkerHost is not null && IsHostStopped(_inventoryWorkerHost))
        {
            _inventoryWorkerHost.Dispose();
            _inventoryWorkerHost = null;
        }

        if (_inventoryWorkerHost is not null)
        {
            return;
        }

        _inventoryWorkerHost = await StartInventoryWorkerAsync(cancellationToken);
        await Task.Delay(WorkerStartupStabilizationDelay, cancellationToken);
    }

    public async Task StopPaymentWorkerAsync(CancellationToken cancellationToken)
    {
        if (_paymentWorkerHost is null)
        {
            return;
        }

        await _paymentWorkerHost.StopAsync(cancellationToken);
        _paymentWorkerHost.Dispose();
        _paymentWorkerHost = null;
    }

    public async Task EnsurePaymentWorkerStartedAsync(CancellationToken cancellationToken)
    {
        if (_paymentWorkerHost is not null && IsHostStopped(_paymentWorkerHost))
        {
            _paymentWorkerHost.Dispose();
            _paymentWorkerHost = null;
        }

        if (_paymentWorkerHost is not null)
        {
            return;
        }

        _paymentWorkerHost = await StartPaymentWorkerAsync(cancellationToken);
        await Task.Delay(WorkerStartupStabilizationDelay, cancellationToken);
    }

    public async Task RestartWorkersAsync(CancellationToken cancellationToken)
    {
        await StopPaymentWorkerAsync(cancellationToken);
        await StopInventoryWorkerAsync(cancellationToken);
        await EnsurePaymentWorkerStartedAsync(cancellationToken);
        await EnsureInventoryWorkerStartedAsync(cancellationToken);
        await Task.Delay(WorkerStartupStabilizationDelay, cancellationToken);
        await EnsureHappyPathWorkersReadyAsync(cancellationToken);
    }

    private async Task EnsureHappyPathWorkersReadyAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var attempts = 0;
        Guid? lastProbeOrderId = null;
        string? lastObservedStatus = null;

        while (DateTimeOffset.UtcNow - started < WorkerReadinessProbeTimeout)
        {
            attempts++;
            var probeOrderId = await CreateOrderAsync(12.34m, $"readiness-{Guid.NewGuid():N}@example.com", cancellationToken);
            lastProbeOrderId = probeOrderId;

            var confirmed = await WaitForOrderReadModelStatusAsync(
                probeOrderId,
                expectedStatus: OrderStatuses.Confirmed,
                timeout: TimeSpan.FromSeconds(20),
                cancellationToken);

            if (confirmed)
            {
                return;
            }

            lastObservedStatus = (await GetOrderByIdAsync(probeOrderId, cancellationToken))?.Status ?? "<not-found>";
            await Task.Delay(1000, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Worker readiness probe failed after {attempts} attempt(s) and {WorkerReadinessProbeTimeout.TotalSeconds:F0}s. Last probe order: {lastProbeOrderId?.ToString() ?? "<none>"}, last observed status: {lastObservedStatus ?? "<unknown>"}.");
    }

    public async Task<HttpStatusCode> GetOrderStatusCodeAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var response = await OrderClient.GetAsync($"/orders/{orderId}", cancellationToken);
        return response.StatusCode;
    }

    public async Task<GetOrderByIdResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var response = await OrderClient.GetAsync($"/orders/{orderId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GetOrderByIdResponse>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<GetOrdersResponse>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var response = await OrderClient.GetAsync($"/orders?page={page}&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<GetOrdersResponse>>(cancellationToken: cancellationToken);
        return payload ?? Array.Empty<GetOrdersResponse>();
    }

    public async Task<bool> WaitForOrderReadModelStatusAsync(Guid orderId, string expectedStatus, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            var response = await GetOrderByIdAsync(orderId, cancellationToken);
            if (response is not null && string.Equals(response.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
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
                var routingKey = TopicRoutingKeyHelper.GenerateRoutingKey(
                    @event.SourceService,
                    @event.Entity,
                    @event.Action);

                context.SetRoutingKey(routingKey);
                if (context.TryGetPayload<RabbitMqSendContext>(out var rabbitMqSendContext))
                {
                    rabbitMqSendContext.RoutingKey = routingKey;
                }
            }, cancellationToken);
        }
        finally
        {
            await bus.StopAsync(cancellationToken);
            if (bus is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (bus is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public async Task PublishPaymentFailedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var hostUri = new Uri($"rabbitmq://{GetRequiredSetting("Messaging:RabbitMq:Host")}:{GetRequiredSetting("Messaging:RabbitMq:Port")}");

        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(hostUri, h =>
            {
                h.Username(GetRequiredSetting("Messaging:RabbitMq:UserName"));
                h.Password(GetRequiredSetting("Messaging:RabbitMq:Password"));
            });

            cfg.Message<EventContext<PaymentFailed>>(x => x.SetEntityName(OrderMessagingTopology.ExchangeName));
            cfg.Publish<EventContext<PaymentFailed>>(x => x.ExchangeType = "topic");
        });

        await bus.StartAsync(cancellationToken);
        try
        {
            var @event = EventContext.Create(
                sourceService: OrderMessagingTopology.SourceService,
                entity: OrderMessagingTopology.EntityName,
                action: OrderMessagingTopology.Actions.PaymentFailed,
                payload: new PaymentFailed(orderId));

            await bus.Publish(@event, context =>
            {
                var routingKey = TopicRoutingKeyHelper.GenerateRoutingKey(
                    @event.SourceService,
                    @event.Entity,
                    @event.Action);

                context.SetRoutingKey(routingKey);
                if (context.TryGetPayload<RabbitMqSendContext>(out var rabbitMqSendContext))
                {
                    rabbitMqSendContext.RoutingKey = routingKey;
                }
            }, cancellationToken);
        }
        finally
        {
            await bus.StopAsync(cancellationToken);
            if (bus is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (bus is IDisposable disposable)
            {
                disposable.Dispose();
            }
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
        var sagaInstanceObserved = false;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            await using var connection = new NpgsqlConnection(BuildDatabaseConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM \"OrderState\" WHERE \"CorrelationId\" = @orderId";
            command.Parameters.AddWithValue("orderId", orderId);

            var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
            if (count > 0)
            {
                sagaInstanceObserved = true;
            }
            else if (sagaInstanceObserved)
            {
                return true;
            }
            else
            {
                await using var statusCommand = connection.CreateCommand();
                statusCommand.CommandText = "SELECT \"Status\" FROM \"Orders\" WHERE \"OrderId\" = @orderId";
                statusCommand.Parameters.AddWithValue("orderId", orderId);

                var status = (await statusCommand.ExecuteScalarAsync(cancellationToken))?.ToString();
                if (string.Equals(status, OrderStatuses.Confirmed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
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
            registerConsumers: x =>
            {
                x.AddConsumer<ProcessPaymentConsumer, ProcessPaymentConsumerDefinition>();
                x.AddConsumer<RefundPaymentConsumer, RefundPaymentConsumerDefinition>();
            },
            configureReceiveEndpoints: (cfg, context, _) => cfg.ConfigureEndpoints(context));

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
            registerConsumers: x =>
            {
                x.AddConsumer<ReserveInventoryConsumer, ReserveInventoryConsumerDefinition>();
            },
            configureReceiveEndpoints: (cfg, context, _) => cfg.ConfigureEndpoints(context));

        var host = builder.Build();
        await host.StartAsync(cancellationToken);

        return host;
    }

    private async Task WaitForOrderServiceReadinessAsync(CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(60);
        var started = DateTimeOffset.UtcNow;
        var attempts = 0;
        var lastObservation = "No response observed";

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            attempts++;
            try
            {
                var response = await OrderClient.GetAsync("/alive", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                lastObservation = $"HTTP {(int)response.StatusCode} {response.StatusCode}. Body: {body}";
            }
            catch (HttpRequestException ex)
            {
                lastObservation = $"HttpRequestException: {ex.Message}";
            }
            catch (OperationCanceledException ex)
            {
                lastObservation = $"OperationCanceledException: {ex.Message}";
            }
            catch (Exception ex)
            {
                lastObservation = $"{ex.GetType().Name}: {ex.Message}";
            }

            await Task.Delay(500, cancellationToken);
        }

        var elapsed = DateTimeOffset.UtcNow - started;
        throw new InvalidOperationException(
            $"Order Service did not become alive within {timeout.TotalSeconds:F0} seconds (tried {attempts} times, elapsed {elapsed.TotalSeconds:F2}s). Last observation: {lastObservation}");
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

    private static bool IsHostStopped(IHost host)
    {
        var lifetime = host.Services.GetService<IHostApplicationLifetime>();
        return lifetime?.ApplicationStopped.IsCancellationRequested ?? false;
    }

    private sealed class OrderServiceFactory(Dictionary<string, string?> settings)
        : WebApplicationFactory<OrderServiceEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Set environment to Test so Program.cs doesn't run migrations
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(settings);
            });
        }
    }
}
