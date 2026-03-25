using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Features.Orders.CreateOrder;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_should_succeed_for_valid_command()
    {
        var command = new CreateOrderCommand(149.90m, "customer@example.com");

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_should_fail_when_amount_is_zero_or_negative()
    {
        var zeroAmount = new CreateOrderCommand(0m, "customer@example.com");
        var negativeAmount = new CreateOrderCommand(-10m, "customer@example.com");

        var zeroResult = _validator.Validate(zeroAmount);
        var negativeResult = _validator.Validate(negativeAmount);

        zeroResult.IsValid.ShouldBeFalse();
        negativeResult.IsValid.ShouldBeFalse();
        zeroResult.Errors.Any(e => e.PropertyName == nameof(CreateOrderCommand.Amount)).ShouldBeTrue();
        negativeResult.Errors.Any(e => e.PropertyName == nameof(CreateOrderCommand.Amount)).ShouldBeTrue();
    }

    [Fact]
    public void Validate_should_fail_when_customer_email_is_missing_or_invalid()
    {
        var emptyEmail = new CreateOrderCommand(25m, string.Empty);
        var invalidEmail = new CreateOrderCommand(25m, "invalid-email");

        var emptyResult = _validator.Validate(emptyEmail);
        var invalidResult = _validator.Validate(invalidEmail);

        emptyResult.IsValid.ShouldBeFalse();
        invalidResult.IsValid.ShouldBeFalse();
        emptyResult.Errors.Any(e => e.PropertyName == nameof(CreateOrderCommand.CustomerEmail)).ShouldBeTrue();
        invalidResult.Errors.Any(e => e.PropertyName == nameof(CreateOrderCommand.CustomerEmail)).ShouldBeTrue();
    }

    [Fact]
    public void Validate_should_accept_edge_amount_with_many_decimal_places()
    {
        var command = new CreateOrderCommand(0.0000001m, "edge@example.com");

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
