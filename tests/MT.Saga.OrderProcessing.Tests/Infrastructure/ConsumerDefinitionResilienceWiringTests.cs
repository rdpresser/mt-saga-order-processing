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

    [Theory]
    [InlineData("src/Services/MT.Saga.OrderProcessing.PaymentService/Consumers/Definitions/ProcessPaymentConsumerDefinition.cs")]
    [InlineData("src/Services/MT.Saga.OrderProcessing.PaymentService/Consumers/Definitions/RefundPaymentConsumerDefinition.cs")]
    [InlineData("src/Services/MT.Saga.OrderProcessing.InventoryService/Consumers/Definitions/ReserveInventoryConsumerDefinition.cs")]
    [InlineData("src/MT.Saga.OrderProcessing.Infrastructure/Messaging/Consumers/Definitions/OrderReadModelProjectorConsumerDefinition.cs")]
    public void ConsumerDefinitions_should_apply_prefetch_count_from_resilience_options(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var filePath = Path.Combine(repositoryRoot, relativePath);
        var content = File.ReadAllText(filePath);

        content.ShouldContain("endpointConfigurator.PrefetchCount = _options.PrefetchCount;");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MT.Saga.AppHost.Aspire.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located from test execution directory.");
    }
}
