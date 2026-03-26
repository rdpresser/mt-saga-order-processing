using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Features.Orders.GetOrders;

public class GetOrdersQueryValidatorTests
{
    [Fact]
    public void Validate_should_fail_when_page_is_zero()
    {
        var validator = new GetOrdersQueryValidator();
        var query = new GetOrdersQuery(Page: 0, PageSize: 20);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.Any(e => e.PropertyName == nameof(GetOrdersQuery.Page)).ShouldBeTrue();
    }

    [Fact]
    public void Validate_should_fail_when_page_size_is_out_of_range()
    {
        var validator = new GetOrdersQueryValidator();
        var query = new GetOrdersQuery(Page: 1, PageSize: 0);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.Any(e => e.PropertyName == nameof(GetOrdersQuery.PageSize)).ShouldBeTrue();
    }

    [Fact]
    public void Validate_should_succeed_when_query_is_valid()
    {
        var validator = new GetOrdersQueryValidator();
        var query = new GetOrdersQuery(Page: 1, PageSize: 50);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }
}
