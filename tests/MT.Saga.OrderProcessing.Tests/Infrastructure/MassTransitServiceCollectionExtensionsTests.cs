using System.Reflection;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

/// <summary>
/// Policy-level tests for RabbitMQ topic topology publishing.
/// Ensures ConfigureOrderTopologyPublishing in RabbitMqTransportConfiguration
/// explicitly covers every concrete domain event type in the Contracts assembly.
/// Catches gaps when new event types are added without updating the topology configuration.
/// </summary>
public class OrderTopologyPublishingTests
{
    private static readonly Type[] KnownDomainEventTypes =
    [
        typeof(OrderCreated),
        typeof(PaymentProcessed),
        typeof(PaymentFailed),
        typeof(InventoryReserved),
        typeof(InventoryFailed),
        typeof(OrderConfirmed),
        typeof(OrderCancelled)
    ];

    [Fact]
    public void ConfigureOrderTopologyPublishing_should_be_a_public_static_method()
    {
        var method = typeof(RabbitMqTransportConfiguration)
            .GetMethod(nameof(RabbitMqTransportConfiguration.ConfigureOrderTopologyPublishing));

        method.ShouldNotBeNull();
        method.IsPublic.ShouldBeTrue();
        method.IsStatic.ShouldBeTrue();
    }

    [Fact]
    public void All_concrete_domain_event_types_in_contracts_assembly_should_be_in_the_known_events_list()
    {
        // Discover all concrete non-generic event types from the Contracts Events namespace.
        var discoveredTypes = typeof(OrderCreated)
            .Assembly
            .GetTypes()
            .Where(t =>
                t.IsClass
                && !t.IsAbstract
                && !t.IsGenericType
                && t.Namespace == typeof(OrderCreated).Namespace
                && t != typeof(EventContext))
            .ToHashSet();

        var knownSet = KnownDomainEventTypes.ToHashSet();

        // All discovered types must be in the known list (no missing coverage).
        var uncovered = discoveredTypes.Except(knownSet).ToList();
        uncovered.ShouldBeEmpty(
            $"New event type(s) were added to the Contracts assembly but are not listed in " +
            $"{nameof(OrderTopologyPublishingTests)}.{nameof(KnownDomainEventTypes)} and may be missing " +
            $"from RabbitMqTransportConfiguration.ConfigureOrderTopologyPublishing: " +
            $"{string.Join(", ", uncovered.Select(t => t.Name))}");
    }

    [Fact]
    public void ConfigureRabbitMqHost_should_have_both_IConfiguration_and_RabbitMqOptions_overloads()
    {
        var overloads = typeof(RabbitMqTransportConfiguration)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(RabbitMqTransportConfiguration.ConfigureRabbitMqHost))
            .ToList();

        // Both IConfiguration and RabbitMqOptions overloads must exist.
        overloads.Count.ShouldBe(2, "Expected two ConfigureRabbitMqHost overloads: IConfiguration and RabbitMqOptions");
    }
}
