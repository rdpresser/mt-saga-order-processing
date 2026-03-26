using System.Reflection;
using System.ComponentModel;
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

    [Fact]
    public void ConfigureTopicMessageTopologyGeneric_should_be_public_for_safe_reflection()
    {
        var publicMethod = typeof(MassTransitServiceCollectionExtensions)
            .GetMethod("ConfigureTopicMessageTopologyGeneric", BindingFlags.Public | BindingFlags.Static);

        publicMethod.ShouldNotBeNull();

        var editorBrowsable = publicMethod.GetCustomAttribute<EditorBrowsableAttribute>();
        editorBrowsable.ShouldNotBeNull();
        editorBrowsable.State.ShouldBe(EditorBrowsableState.Never);
    }

    [Fact]
    public void ConfigureTopicMessageTopologyGeneric_should_be_resolvable_for_reflection_closure()
    {
        var openGenericMethod = typeof(MassTransitServiceCollectionExtensions)
            .GetMethod("ConfigureTopicMessageTopologyGeneric", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not locate ConfigureTopicMessageTopologyGeneric method.");

        var closedMethod = openGenericMethod.MakeGenericMethod(typeof(OrderCreated));

        closedMethod.IsGenericMethodDefinition.ShouldBeFalse();
        closedMethod.GetGenericArguments().Single().ShouldBe(typeof(OrderCreated));
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
