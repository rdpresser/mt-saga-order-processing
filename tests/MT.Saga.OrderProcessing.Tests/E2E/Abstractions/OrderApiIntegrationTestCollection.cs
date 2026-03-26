namespace MT.Saga.OrderProcessing.Tests.E2E.Abstractions;

[CollectionDefinition(nameof(OrderApiIntegrationTestCollection), DisableParallelization = true)]
public sealed class OrderApiIntegrationTestCollection : ICollectionFixture<FullSagaE2EFixture>
{
}