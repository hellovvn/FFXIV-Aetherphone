using System.Net;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TransientFailuresTests
{
    [Theory]
    [InlineData(502)]
    [InlineData(504)]
    [InlineData(520)]
    [InlineData(522)]
    [InlineData(524)]
    [InlineData(527)]
    [InlineData(530)]
    public void A_gateway_failure_is_worth_another_try(int status)
    {
        Assert.True(TransientFailures.IsGatewayStatus((HttpStatusCode)status));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(519)]
    [InlineData(528)]
    [InlineData(529)]
    public void Any_other_status_is_left_to_the_caller(int status)
    {
        Assert.False(TransientFailures.IsGatewayStatus((HttpStatusCode)status));
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.ResponseEnded)]
    public void A_dropped_connection_is_worth_another_try(HttpRequestError error)
    {
        Assert.True(TransientFailures.IsDroppedConnection(new HttpRequestException(error)));
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.ConfigurationLimitExceeded)]
    [InlineData(HttpRequestError.Unknown)]
    public void Other_request_errors_are_left_to_the_caller(HttpRequestError error)
    {
        Assert.False(TransientFailures.IsDroppedConnection(new HttpRequestException(error)));
    }

    [Fact]
    public void A_timeout_is_not_retried()
    {
        Assert.False(TransientFailures.IsDroppedConnection(new TaskCanceledException()));
    }
}
