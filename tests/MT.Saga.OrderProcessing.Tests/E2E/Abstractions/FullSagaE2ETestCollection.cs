using Xunit;

namespace MT.Saga.OrderProcessing.Tests.E2E.Abstractions;

[CollectionDefinition(nameof(FullSagaE2ETestCollection), DisableParallelization = true)]
public sealed class FullSagaE2ETestCollection : ICollectionFixture<FullSagaE2EFixture>
{
}
