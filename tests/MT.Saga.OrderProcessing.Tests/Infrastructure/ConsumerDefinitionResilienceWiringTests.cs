using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.InventoryService.Consumers.Definitions;
using MT.Saga.OrderProcessing.PaymentService.Consumers.Definitions;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers.Definitions;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class ConsumerDefinitionResilienceWiringTests
{
    [Theory]
    [InlineData(typeof(ProcessPaymentConsumerDefinition))]
    [InlineData(typeof(RefundPaymentConsumerDefinition))]
    [InlineData(typeof(ReserveInventoryConsumerDefinition))]
    [InlineData(typeof(OrderReadModelProjectorConsumerDefinition))]
    public void ConsumerDefinitions_should_depend_on_resilience_options_provider(Type definitionType)
    {
        var ctor = definitionType.GetConstructors().Single();

        ctor.IsPublic.ShouldBeTrue();
        ctor.GetParameters().Length.ShouldBe(1);
        ctor.GetParameters()[0].ParameterType.ShouldBe(typeof(IMessagingResilienceOptionsProvider));
    }
}
