using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class EndpointPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_should_call_handler_directly_when_no_behaviors()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var pipeline = new EndpointPipeline<FakeRequest, string>(
            Enumerable.Empty<IEndpointBehavior<FakeRequest, string>>());
        var handlerCalled = false;

        var result = await pipeline.ExecuteAsync(new FakeRequest("test"), ct, () =>
        {
            handlerCalled = true;
            return Task.FromResult("handler-result");
        });

        result.ShouldBe("handler-result");
        handlerCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_should_wrap_handler_with_single_behavior()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tracker = new ExecutionTracker();
        var behavior = new TrackingBehavior("B1", tracker);
        var pipeline = new EndpointPipeline<FakeRequest, string>([behavior]);

        var result = await pipeline.ExecuteAsync(new FakeRequest("test"), ct, () =>
        {
            tracker.Record("handler");
            return Task.FromResult("ok");
        });

        result.ShouldBe("ok");
        tracker.Steps.ShouldBe(["B1:before", "handler", "B1:after"]);
    }

    [Fact]
    public async Task ExecuteAsync_should_run_behaviors_in_registration_order()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tracker = new ExecutionTracker();
        var behavior1 = new TrackingBehavior("B1", tracker);
        var behavior2 = new TrackingBehavior("B2", tracker);
        var behavior3 = new TrackingBehavior("B3", tracker);
        var pipeline = new EndpointPipeline<FakeRequest, string>([behavior1, behavior2, behavior3]);

        var result = await pipeline.ExecuteAsync(new FakeRequest("test"), ct, () =>
        {
            tracker.Record("handler");
            return Task.FromResult("ok");
        });

        result.ShouldBe("ok");
        tracker.Steps.ShouldBe([
            "B1:before", "B2:before", "B3:before",
            "handler",
            "B3:after", "B2:after", "B1:after"
        ]);
    }

    [Fact]
    public async Task ExecuteAsync_should_short_circuit_when_behavior_throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tracker = new ExecutionTracker();
        var behavior1 = new TrackingBehavior("B1", tracker);
        var throwingBehavior = new ThrowingBehavior();
        var behavior3 = new TrackingBehavior("B3", tracker);
        var pipeline = new EndpointPipeline<FakeRequest, string>(
            [behavior1, throwingBehavior, behavior3]);
        var handlerCalled = false;

        var act = () => pipeline.ExecuteAsync(new FakeRequest("test"), ct, () =>
        {
            handlerCalled = true;
            return Task.FromResult("ok");
        });

        await act.ShouldThrowAsync<InvalidOperationException>();
        handlerCalled.ShouldBeFalse();
        tracker.Steps.ShouldBe(["B1:before"]);
    }

    private sealed record FakeRequest(string Value);

    private sealed class ExecutionTracker
    {
        private readonly List<string> _steps = [];
        public IReadOnlyList<string> Steps => _steps;
        public void Record(string step) => _steps.Add(step);
    }

    private sealed class TrackingBehavior(string name, ExecutionTracker tracker)
        : IEndpointBehavior<FakeRequest, string>
    {
        public async Task<string> Handle(FakeRequest request, CancellationToken ct, Func<Task<string>> next)
        {
            tracker.Record($"{name}:before");
            var result = await next().ConfigureAwait(false);
            tracker.Record($"{name}:after");
            return result;
        }
    }

    private sealed class ThrowingBehavior : IEndpointBehavior<FakeRequest, string>
    {
        public Task<string> Handle(FakeRequest request, CancellationToken ct, Func<Task<string>> next)
            => throw new InvalidOperationException("short-circuit");
    }
}
