using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class MessagingResilienceOptionsRegistrationTests
{
    [Fact]
    public void AddMassTransitPoliciesOptions_should_bind_resilience_options_and_expose_provider()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Messaging:Resilience:PrefetchCount"] = "32",
            ["Messaging:Resilience:ConcurrentMessageLimit"] = "12",
            ["Messaging:Resilience:MaxRetryAttempts"] = "7",
            ["Messaging:Resilience:PublishMaxAttempts"] = "4",
            ["Messaging:Resilience:PublishRetryDelayMilliseconds"] = "150",
            ["Messaging:Resilience:KillSwitchActivationThreshold"] = "9",
            ["Messaging:Resilience:KillSwitchTripThreshold"] = "0.25",
            ["Messaging:Resilience:KillSwitchRestartTimeout"] = "00:02:00",
            ["Messaging:RabbitMq:Host"] = "localhost",
            ["Messaging:RabbitMq:Port"] = "5672",
            ["Messaging:RabbitMq:UserName"] = "guest",
            ["Messaging:RabbitMq:Password"] = "guest",
            ["Messaging:RabbitMq:VirtualHost"] = "/"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddMassTransitPoliciesOptions(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var optionsProvider = provider.GetRequiredService<IMessagingResilienceOptionsProvider>();
        var options = optionsProvider.Current;

        options.PrefetchCount.ShouldBe(32);
        options.ConcurrentMessageLimit.ShouldBe(12);
        options.MaxRetryAttempts.ShouldBe(7);
        options.PublishMaxAttempts.ShouldBe(4);
        options.PublishRetryDelayMilliseconds.ShouldBe(150);
        options.KillSwitchActivationThreshold.ShouldBe(9);
        options.KillSwitchTripThreshold.ShouldBe(0.25);
        options.KillSwitchRestartTimeout.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void AddMassTransitPoliciesOptions_should_fail_validation_for_invalid_resilience_options()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Messaging:Resilience:PrefetchCount"] = "0",
            ["Messaging:Resilience:ConcurrentMessageLimit"] = "0",
            ["Messaging:Resilience:MaxRetryAttempts"] = "0",
            ["Messaging:Resilience:PublishMaxAttempts"] = "0",
            ["Messaging:Resilience:PublishRetryDelayMilliseconds"] = "0",
            ["Messaging:Resilience:KillSwitchActivationThreshold"] = "0",
            ["Messaging:Resilience:KillSwitchTripThreshold"] = "2",
            ["Messaging:Resilience:KillSwitchRestartTimeout"] = "00:00:00"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddMassTransitPoliciesOptions(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var exception = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<MessagingResilienceOptions>>().Value);

        exception.Failures.ShouldNotBeEmpty();
    }
}
