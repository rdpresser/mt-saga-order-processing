using NetArchTest.Rules;
using Shouldly;
using TestResult = NetArchTest.Rules.TestResult;

namespace MT.Saga.OrderProcessing.Architecture.Tests.Layers;

public sealed class LayerDependencyTests : BaseTest
{
    [Fact]
    public void Contracts_ShouldNotDependOn_OtherSolutionAssemblies()
    {
        var result = Types.InAssembly(ContractsAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                SagaAssembly.GetName().Name!,
                InfrastructureAssembly.GetName().Name!,
                OrderServiceAssembly.GetName().Name!,
                PaymentServiceAssembly.GetName().Name!,
                InventoryServiceAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Saga_ShouldNotDependOn_Infrastructure_Or_ServiceAssemblies()
    {
        TestResult result = Types.InAssembly(SagaAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                InfrastructureAssembly.GetName().Name!,
                OrderServiceAssembly.GetName().Name!,
                PaymentServiceAssembly.GetName().Name!,
                InventoryServiceAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_ServiceAssemblies()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                OrderServiceAssembly.GetName().Name!,
                PaymentServiceAssembly.GetName().Name!,
                InventoryServiceAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void OrderService_ShouldNotDependOn_Saga_Or_WorkerServices()
    {
        TestResult result = Types.InAssembly(OrderServiceAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                SagaAssembly.GetName().Name!,
                PaymentServiceAssembly.GetName().Name!,
                InventoryServiceAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void PaymentService_ShouldNotDependOn_Saga_OrderService_Or_InventoryService()
    {
        TestResult result = Types.InAssembly(PaymentServiceAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                SagaAssembly.GetName().Name!,
                OrderServiceAssembly.GetName().Name!,
                InventoryServiceAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void InventoryService_ShouldNotDependOn_Saga_OrderService_Or_PaymentService()
    {
        TestResult result = Types.InAssembly(InventoryServiceAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                SagaAssembly.GetName().Name!,
                OrderServiceAssembly.GetName().Name!,
                PaymentServiceAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
