using FluentValidation;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

public sealed class GetOrderByIdQueryValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdQueryValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");
    }
}
