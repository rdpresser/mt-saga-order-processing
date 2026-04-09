using MT.Saga.OrderProcessing.Contracts.Events;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Contracts;

public class EventContextTests
{
    private record TestPayload(string Value);

    [Fact]
    public void Create_should_populate_all_required_fields()
    {
        var payload = new TestPayload("test-value");

        var result = EventContext.Create("my-service", "order", "created", payload);

        result.SourceService.ShouldBe("my-service");
        result.Entity.ShouldBe("order");
        result.Action.ShouldBe("created");
        result.Payload.ShouldBe(payload);
    }

    [Fact]
    public void Create_should_generate_non_empty_event_id()
    {
        var result = EventContext.Create("svc", "entity", "action", new TestPayload("v"));

        result.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_should_set_occurred_at_utc_close_to_now()
    {
        var before = DateTimeOffset.UtcNow;

        var result = EventContext.Create("svc", "entity", "action", new TestPayload("v"));

        var after = DateTimeOffset.UtcNow;
        result.OccurredAtUtc.ShouldBeGreaterThanOrEqualTo(before);
        result.OccurredAtUtc.ShouldBeLessThanOrEqualTo(after.AddSeconds(2));
    }

    [Fact]
    public void EventType_should_return_payload_type_name()
    {
        var result = EventContext.Create("svc", "entity", "action", new TestPayload("v"));

        result.EventType.ShouldBe(nameof(TestPayload));
    }

    [Fact]
    public void AggregateType_should_return_entity()
    {
        var result = EventContext.Create("svc", "my-entity", "action", new TestPayload("v"));

        result.AggregateType.ShouldBe("my-entity");
    }

    [Fact]
    public void Create_should_default_optional_fields_correctly()
    {
        var result = EventContext.Create("svc", "entity", "action", new TestPayload("v"));

        result.CorrelationId.ShouldBeNull();
        result.CausationId.ShouldBeNull();
        result.UserId.ShouldBeNull();
        result.IsAuthenticated.ShouldBeFalse();
        result.Version.ShouldBe(1);
        result.Metadata.ShouldBeNull();
    }
}
