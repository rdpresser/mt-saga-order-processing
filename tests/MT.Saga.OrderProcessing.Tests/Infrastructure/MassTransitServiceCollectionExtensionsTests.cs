using System.Reflection;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class MassTransitServiceCollectionExtensionsTests
{
    [Fact]
    public void DiscoverEventPayloadTypes_should_include_all_domain_events()
    {
        var payloadTypes = InvokeDiscoverEventPayloadTypes();

        payloadTypes.ShouldContain(typeof(OrderCreated));
        payloadTypes.ShouldContain(typeof(PaymentProcessed));
        payloadTypes.ShouldContain(typeof(PaymentFailed));
        payloadTypes.ShouldContain(typeof(InventoryReserved));
        payloadTypes.ShouldContain(typeof(InventoryFailed));
        payloadTypes.ShouldContain(typeof(OrderConfirmed));
        payloadTypes.ShouldContain(typeof(OrderCancelled));
    }

    [Fact]
    public void DiscoverEventPayloadTypes_should_exclude_event_context_helper_types()
    {
        var payloadTypes = InvokeDiscoverEventPayloadTypes();

        payloadTypes.ShouldNotContain(typeof(EventContext));
        payloadTypes.ShouldNotContain(typeof(EventContext<OrderCreated>));
    }

    private static IReadOnlyCollection<Type> InvokeDiscoverEventPayloadTypes()
    {
        var method = typeof(MassTransitServiceCollectionExtensions)
            .GetMethod("DiscoverEventPayloadTypes", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not locate DiscoverEventPayloadTypes method.");

        var result = method.Invoke(null, null) as IEnumerable<Type>
            ?? throw new InvalidOperationException("DiscoverEventPayloadTypes returned an invalid result.");

        return result.ToArray();
    }
}
