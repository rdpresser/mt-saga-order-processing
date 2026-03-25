using System.Reflection;
using MT.Saga.OrderProcessing.OrderService.Extensions;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Services.OrderService;

public sealed class ApplicationBuilderExtensionsTests
{
    [Theory]
    [InlineData(null, null, "/")]
    [InlineData("", "", "/")]
    [InlineData("/", null, "/")]
    [InlineData("/ingress", "/configured", "/ingress")]
    [InlineData("", "/configured", "/configured")]
    [InlineData("configured", null, "/configured")]
    public void ResolveSwaggerServerUrl_should_return_expected_url(string? requestPathBase, string? configuredPathBase, string expected)
    {
        var method = typeof(ApplicationBuilderExtensions)
            .GetMethod("ResolveSwaggerServerUrl", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not locate ResolveSwaggerServerUrl method.");

        var result = method.Invoke(null, [requestPathBase, configuredPathBase]) as string;

        result.ShouldBe(expected);
    }
}
