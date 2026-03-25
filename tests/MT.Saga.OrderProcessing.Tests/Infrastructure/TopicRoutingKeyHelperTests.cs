using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class TopicRoutingKeyHelperTests
{
    [Fact]
    public void GenerateRoutingKey_should_use_lowercase_dot_notation()
    {
        var routingKey = TopicRoutingKeyHelper.GenerateRoutingKey("Orders", "Order", "Payment-Processed");

        routingKey.ShouldBe("orders.order.payment-processed");
    }

    [Fact]
    public void GenerateWildcardBindingKey_should_match_any_action_suffix()
    {
        var bindingKey = TopicRoutingKeyHelper.GenerateWildcardBindingKey("orders", "order");

        bindingKey.ShouldBe("orders.order.*");
    }

    [Theory]
    [InlineData("", "order", "created")]
    [InlineData("orders", "", "created")]
    [InlineData("orders", "order", "")]
    [InlineData("   ", "order", "created")]
    public void GenerateRoutingKey_should_throw_for_invalid_segments(string sourceService, string entity, string action)
    {
        var act = () => TopicRoutingKeyHelper.GenerateRoutingKey(sourceService, entity, action);

        act.ShouldThrow<ArgumentException>();
    }
}
