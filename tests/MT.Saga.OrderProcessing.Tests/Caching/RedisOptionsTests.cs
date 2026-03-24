using MT.Saga.OrderProcessing.Infrastructure.Caching;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Caching;

public class RedisOptionsTests
{
    [Theory]
    [InlineData(true, "ssl=true")]
    [InlineData(false, "ssl=false")]
    public void ConnectionString_should_include_explicit_ssl_literal(bool secure, string expectedSslPart)
    {
        var options = new RedisOptions
        {
            Host = "redis",
            Port = 6380,
            Secure = secure,
            Password = string.Empty
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldContain(expectedSslPart);
    }

    [Fact]
    public void ConnectionString_should_not_include_password_when_empty()
    {
        var options = new RedisOptions
        {
            Host = "redis",
            Port = 6379,
            Secure = false,
            Password = string.Empty
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldNotContain("password=");
        connectionString.ShouldContain("ssl=false");
        connectionString.ShouldContain("abortConnect=False");
    }

    [Fact]
    public void ConnectionString_should_include_password_when_provided()
    {
        var options = new RedisOptions
        {
            Host = "redis",
            Port = 6379,
            Secure = true,
            Password = "secret"
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldContain("password=secret");
        connectionString.ShouldContain("ssl=true");
        connectionString.ShouldContain("abortConnect=False");
    }

    [Fact]
    public void ConnectionString_should_follow_expected_format()
    {
        var options = new RedisOptions
        {
            Host = "cache-host",
            Port = 6381,
            Secure = true,
            Password = "p@ss"
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldBe("cache-host:6381,password=p@ss,ssl=true,abortConnect=False");
    }
}
