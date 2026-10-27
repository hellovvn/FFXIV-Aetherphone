using System.Net;

namespace Aetherphone.Core.Net;

internal static class TransientFailures
{
    private const int FirstEdgeOriginStatus = 520;
    private const int LastEdgeOriginStatus = 527;
    private const int EdgeOriginUnreachableStatus = 530;

    public static bool IsGatewayStatus(HttpStatusCode status)
    {
        var code = (int)status;
        return status is HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout
            || code is >= FirstEdgeOriginStatus and <= LastEdgeOriginStatus
            || code == EdgeOriginUnreachableStatus;
    }

    public static bool IsDroppedConnection(Exception exception)
    {
        return exception is HttpRequestException
        {
            HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded,
        };
    }
}
