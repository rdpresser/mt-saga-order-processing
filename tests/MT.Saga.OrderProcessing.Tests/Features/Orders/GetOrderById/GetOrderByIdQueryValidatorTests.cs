using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Features.Orders.GetOrderById;

public class GetOrderByIdQueryValidatorTests
{
    [Fact]
    public void Validate_should_fail_when_order_id_is_empty()
    {
        var validator = new GetOrderByIdQueryValidator();
        var query = new GetOrderByIdQuery(Guid.Empty);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.Any(e => e.PropertyName == nameof(GetOrderByIdQuery.OrderId)).ShouldBeTrue();
    }

    [Fact]
    public void Validate_should_succeed_when_order_id_is_valid()
    {
        var validator = new GetOrderByIdQueryValidator();
        var query = new GetOrderByIdQuery(Guid.NewGuid());

        var result = validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }
}
