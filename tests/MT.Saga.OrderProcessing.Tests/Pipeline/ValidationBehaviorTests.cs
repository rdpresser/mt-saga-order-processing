using FluentValidation;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_should_call_next_and_return_result_when_no_validators_registered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var behavior = new ValidationBehavior<FakeRequest, string>(
            Enumerable.Empty<IValidator<FakeRequest>>());
        var handlerCalled = false;

        var result = await behavior.Handle(new FakeRequest("test"), ct, () =>
        {
            handlerCalled = true;
            return Task.FromResult("ok");
        });

        result.ShouldBe("ok");
        handlerCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_should_call_next_and_return_result_when_validation_passes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var validator = new PassingValidator();
        var behavior = new ValidationBehavior<FakeRequest, string>([validator]);
        var handlerCalled = false;

        var result = await behavior.Handle(new FakeRequest("valid"), ct, () =>
        {
            handlerCalled = true;
            return Task.FromResult("success");
        });

        result.ShouldBe("success");
        handlerCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_should_throw_validation_exception_and_not_call_next_when_single_failure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var validator = new FailingValidator("Name", "Name is required");
        var behavior = new ValidationBehavior<FakeRequest, string>([validator]);
        var handlerCalled = false;

        var act = () => behavior.Handle(new FakeRequest("bad"), ct, () =>
        {
            handlerCalled = true;
            return Task.FromResult("never");
        });

        var exception = await act.ShouldThrowAsync<ValidationException>();
        exception.Errors.Count().ShouldBe(1);
        exception.Errors.First().PropertyName.ShouldBe("Name");
        exception.Errors.First().ErrorMessage.ShouldBe("Name is required");
        handlerCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_should_aggregate_errors_from_multiple_validators()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var validator1 = new FailingValidator("Name", "Name is required");
        var validator2 = new FailingValidator("Email", "Email is invalid");
        var behavior = new ValidationBehavior<FakeRequest, string>([validator1, validator2]);
        var handlerCalled = false;

        var act = () => behavior.Handle(new FakeRequest("bad"), ct, () =>
        {
            handlerCalled = true;
            return Task.FromResult("never");
        });

        var exception = await act.ShouldThrowAsync<ValidationException>();
        var errors = exception.Errors.ToList();
        errors.Count.ShouldBe(2);
        errors.ShouldContain(e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required");
        errors.ShouldContain(e => e.PropertyName == "Email" && e.ErrorMessage == "Email is invalid");
        handlerCalled.ShouldBeFalse();
    }

    private sealed record FakeRequest(string Value);

    private sealed class PassingValidator : AbstractValidator<FakeRequest>
    {
    }

    private sealed class FailingValidator : AbstractValidator<FakeRequest>
    {
        public FailingValidator(string propertyName, string errorMessage)
        {
            RuleFor(_ => _)
                .Must(_ => false)
                .WithName(propertyName)
                .WithMessage(errorMessage);
        }
    }
}
