using System.Reflection;
using MT.Saga.OrderProcessing.Contracts.Events;
using Shouldly;

namespace MT.Saga.OrderProcessing.Architecture.Tests.Messaging;

public sealed class EventContextEnvelopeArchitectureTests : BaseTest
{
    [Fact]
    public void Consumers_Should_Use_EventContext_Envelope()
    {
        var invalidConsumers = GetConsumerMessageTypes(PaymentServiceAssembly)
            .Concat(GetConsumerMessageTypes(InventoryServiceAssembly))
            .Concat(GetConsumerMessageTypes(InfrastructureAssembly))
            .Where(x => !IsEventContextType(x.MessageType))
            .Select(x => $"{x.ConsumerType.FullName} -> {x.MessageType.FullName}")
            .ToArray();

        invalidConsumers.ShouldBeEmpty();
    }

    private static IEnumerable<(Type ConsumerType, Type MessageType)> GetConsumerMessageTypes(Assembly assembly)
    {
        var consumerInterfaceName = "MassTransit.IConsumer`1";

        return assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == consumerInterfaceName)
                .Select(i => (ConsumerType: type, MessageType: i.GetGenericArguments()[0])));
    }

    private static bool IsEventContextType(Type messageType)
    {
        return messageType.IsGenericType
            && messageType.GetGenericTypeDefinition() == typeof(EventContext<>);
    }
}
