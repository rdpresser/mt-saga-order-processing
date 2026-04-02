using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Infrastructure.Persistence.Provider;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class ValidateOnStartHostTests
{
    [Fact]
    public async Task ValidateOnStart_should_throw_on_host_startup_when_MessagingResilienceOptions_is_invalid()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Invalid resilience values
                    ["Messaging:Resilience:PrefetchCount"] = "0",
                    ["Messaging:Resilience:MaxRetryAttempts"] = "-1",
                    ["Messaging:Resilience:ConcurrentMessageLimit"] = "0",
                    ["Messaging:Resilience:PublishMaxAttempts"] = "0",
                    ["Messaging:Resilience:PublishRetryDelayMilliseconds"] = "0",
                    ["Messaging:Resilience:KillSwitchActivationThreshold"] = "0",
                    ["Messaging:Resilience:KillSwitchTripThreshold"] = "0",
                    ["Messaging:Resilience:KillSwitchRestartTimeout"] = "00:00:00",
                    // Valid RabbitMq to avoid its validation failing
                    ["Messaging:RabbitMq:Host"] = "localhost",
                    ["Messaging:RabbitMq:Port"] = "5672",
                    ["Messaging:RabbitMq:UserName"] = "guest",
                    ["Messaging:RabbitMq:VirtualHost"] = "/"
                });
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddMassTransitPoliciesOptions(ctx.Configuration);
            })
            .Build();

        var act = () => host.StartAsync(ct);

        await act.ShouldThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task ValidateOnStart_should_throw_on_host_startup_when_PostgresConnectionOptions_is_invalid()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Invalid: empty host and invalid port
                    ["Database:Postgres:Host"] = "",
                    ["Database:Postgres:Port"] = "99999",
                    ["Database:Postgres:Database"] = "test",
                    ["Database:Postgres:UserName"] = "postgres",
                    // Valid pool defaults
                    ["Database:Pool:MinPoolSize"] = "5",
                    ["Database:Pool:MaxPoolSize"] = "10",
                    ["Database:Pool:ConnectionTimeout"] = "30",
                    ["Database:Pool:CommandTimeout"] = "30",
                    ["Database:Pool:KeepAlive"] = "30"
                });
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddOrderProcessingDbContext(ctx.Configuration);
            })
            .Build();

        var act = () => host.StartAsync(ct);

        await act.ShouldThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task ValidateOnStart_should_throw_on_host_startup_when_RabbitMqOptions_is_invalid()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Invalid: empty host and invalid port
                    ["Messaging:RabbitMq:Host"] = "",
                    ["Messaging:RabbitMq:Port"] = "0",
                    ["Messaging:RabbitMq:UserName"] = "",
                    ["Messaging:RabbitMq:VirtualHost"] = "/",
                    // Valid resilience config
                    ["Messaging:Resilience:PrefetchCount"] = "16",
                    ["Messaging:Resilience:MaxRetryAttempts"] = "5",
                    ["Messaging:Resilience:ConcurrentMessageLimit"] = "20",
                    ["Messaging:Resilience:PublishMaxAttempts"] = "3",
                    ["Messaging:Resilience:PublishRetryDelayMilliseconds"] = "200",
                    ["Messaging:Resilience:KillSwitchActivationThreshold"] = "10",
                    ["Messaging:Resilience:KillSwitchTripThreshold"] = "0.15",
                    ["Messaging:Resilience:KillSwitchRestartTimeout"] = "00:01:00"
                });
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddMassTransitPoliciesOptions(ctx.Configuration);
            })
            .Build();

        var act = () => host.StartAsync(ct);

        await act.ShouldThrowAsync<OptionsValidationException>();
    }
}

public class PostgresConnectionOptionsRegistrationTests
{
    [Fact]
    public void AddOrderProcessingDbContext_should_bind_connection_and_pool_options()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Database:Postgres:Host"] = "db-host",
            ["Database:Postgres:Port"] = "5433",
            ["Database:Postgres:UserName"] = "appuser",
            ["Database:Postgres:Password"] = "secret",
            ["Database:Postgres:Database"] = "orders_db",
            ["Database:Postgres:MaintenanceDatabase"] = "admin_db",
            ["Database:Postgres:Schema"] = "orders",
            ["Database:Pool:ConnectionTimeout"] = "60",
            ["Database:Pool:CommandTimeout"] = "45",
            ["Database:Pool:MinPoolSize"] = "3",
            ["Database:Pool:MaxPoolSize"] = "50",
            ["Database:Pool:KeepAlive"] = "15",
            ["Database:Pool:Multiplexing"] = "true",
            ["Database:Pool:IncludeErrorDetail"] = "true"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddOrderProcessingDbContext(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var connOpts = provider.GetRequiredService<IOptions<PostgresConnectionOptions>>().Value;
        connOpts.Host.ShouldBe("db-host");
        connOpts.Port.ShouldBe(5433);
        connOpts.UserName.ShouldBe("appuser");
        connOpts.Database.ShouldBe("orders_db");
        connOpts.MaintenanceDatabase.ShouldBe("admin_db");
        connOpts.Schema.ShouldBe("orders");

        var poolOpts = provider.GetRequiredService<IOptions<PostgresPoolOptions>>().Value;
        poolOpts.ConnectionTimeout.ShouldBe(60);
        poolOpts.CommandTimeout.ShouldBe(45);
        poolOpts.MinPoolSize.ShouldBe(3);
        poolOpts.MaxPoolSize.ShouldBe(50);
        poolOpts.KeepAlive.ShouldBe(15);
        poolOpts.Multiplexing.ShouldBeTrue();
        poolOpts.IncludeErrorDetail.ShouldBeTrue();
    }

    [Fact]
    public void AddOrderProcessingDbContext_should_fail_validation_for_invalid_connection_options()
    {
        var configValues = new Dictionary<string, string?>
        {
            // Invalid: empty host, port out of range, empty DB, empty username
            ["Database:Postgres:Host"] = "",
            ["Database:Postgres:Port"] = "0",
            ["Database:Postgres:Database"] = "",
            ["Database:Postgres:UserName"] = "",
            ["Database:Pool:MinPoolSize"] = "5",
            ["Database:Pool:MaxPoolSize"] = "10",
            ["Database:Pool:ConnectionTimeout"] = "30",
            ["Database:Pool:CommandTimeout"] = "30",
            ["Database:Pool:KeepAlive"] = "30"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddOrderProcessingDbContext(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var exception = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<PostgresConnectionOptions>>().Value);

        exception.Failures.ShouldNotBeEmpty();
    }

    [Fact]
    public void AddOrderProcessingDbContext_should_expose_provider_building_connection_string()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Database:Postgres:Host"] = "pghost",
            ["Database:Postgres:Port"] = "5432",
            ["Database:Postgres:UserName"] = "postgres",
            ["Database:Postgres:Password"] = "pass",
            ["Database:Postgres:Database"] = "orders",
            ["Database:Postgres:MaintenanceDatabase"] = "postgres",
            ["Database:Pool:MinPoolSize"] = "2",
            ["Database:Pool:MaxPoolSize"] = "20",
            ["Database:Pool:ConnectionTimeout"] = "15",
            ["Database:Pool:CommandTimeout"] = "15",
            ["Database:Pool:KeepAlive"] = "10"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOrderProcessingDbContext(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var dbProvider = provider.GetRequiredService<IPostgresDatabaseProvider>();

        dbProvider.ConnectionString.ShouldContain("Host=pghost");
        dbProvider.ConnectionString.ShouldContain("Database=orders");
        dbProvider.MaintenanceConnectionString.ShouldContain("Database=postgres");
    }
}

public class RabbitMqOptionsRegistrationTests
{
    [Fact]
    public void AddMassTransitPoliciesOptions_should_bind_rabbitmq_options()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Messaging:RabbitMq:Host"] = "rabbitmq-host",
            ["Messaging:RabbitMq:Port"] = "5673",
            ["Messaging:RabbitMq:UserName"] = "user",
            ["Messaging:RabbitMq:Password"] = "pass",
            ["Messaging:RabbitMq:VirtualHost"] = "/staging",
            // Valid resilience to avoid its validation
            ["Messaging:Resilience:PrefetchCount"] = "16",
            ["Messaging:Resilience:MaxRetryAttempts"] = "5",
            ["Messaging:Resilience:ConcurrentMessageLimit"] = "20",
            ["Messaging:Resilience:PublishMaxAttempts"] = "3",
            ["Messaging:Resilience:PublishRetryDelayMilliseconds"] = "200",
            ["Messaging:Resilience:KillSwitchActivationThreshold"] = "10",
            ["Messaging:Resilience:KillSwitchTripThreshold"] = "0.15",
            ["Messaging:Resilience:KillSwitchRestartTimeout"] = "00:01:00"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddMassTransitPoliciesOptions(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var options = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        options.Host.ShouldBe("rabbitmq-host");
        options.Port.ShouldBe(5673);
        options.UserName.ShouldBe("user");
        options.VirtualHost.ShouldBe("/staging");
    }

    [Fact]
    public void AddMassTransitPoliciesOptions_should_fail_validation_for_invalid_rabbitmq_options()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Messaging:RabbitMq:Host"] = "",
            ["Messaging:RabbitMq:Port"] = "0",
            ["Messaging:RabbitMq:UserName"] = "",
            ["Messaging:RabbitMq:VirtualHost"] = "",
            // Valid resilience
            ["Messaging:Resilience:PrefetchCount"] = "16",
            ["Messaging:Resilience:MaxRetryAttempts"] = "5",
            ["Messaging:Resilience:ConcurrentMessageLimit"] = "20",
            ["Messaging:Resilience:PublishMaxAttempts"] = "3",
            ["Messaging:Resilience:PublishRetryDelayMilliseconds"] = "200",
            ["Messaging:Resilience:KillSwitchActivationThreshold"] = "10",
            ["Messaging:Resilience:KillSwitchTripThreshold"] = "0.15",
            ["Messaging:Resilience:KillSwitchRestartTimeout"] = "00:01:00"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddMassTransitPoliciesOptions(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var exception = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value);

        exception.Failures.ShouldNotBeEmpty();
    }
}
