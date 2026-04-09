using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_should_call_next_and_return_response()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var logger = NullLoggerFactory.Instance.CreateLogger<LoggingBehavior<FakeRequest, string>>();
        var behavior = new LoggingBehavior<FakeRequest, string>(logger);
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
    public async Task Handle_should_propagate_handler_exception()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var logger = NullLoggerFactory.Instance.CreateLogger<LoggingBehavior<FakeRequest, string>>();
        var behavior = new LoggingBehavior<FakeRequest, string>(logger);

        var act = () => behavior.Handle(new FakeRequest("test"), ct,
            () => Task.FromException<string>(new InvalidOperationException("handler failed")));

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_should_log_handling_and_handled_messages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var fakeLogger = new FakeLogger();
        var behavior = new LoggingBehavior<FakeRequest, string>(fakeLogger);

        await behavior.Handle(new FakeRequest("test"), ct, () => Task.FromResult("ok"));

        fakeLogger.Messages.Count.ShouldBe(2);
        fakeLogger.Messages[0].ShouldContain("Handling");
        fakeLogger.Messages[0].ShouldContain(nameof(FakeRequest));
        fakeLogger.Messages[1].ShouldContain("Handled");
        fakeLogger.Messages[1].ShouldContain(nameof(FakeRequest));
    }

    private sealed record FakeRequest(string Value);

    private sealed class FakeLogger : ILogger<LoggingBehavior<FakeRequest, string>>
    {
        public List<string> Messages { get; } = [];

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
