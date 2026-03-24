using FluentValidation;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .WithMessage("Customer email is required.")
            .EmailAddress()
            .WithMessage("Customer email must be a valid email address.");
    }
}
