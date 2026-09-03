namespace CommunityOS.Host.ApiGateway.Forwarding;

/// <summary>
/// Cached, source-generated logging for the API Gateway (ADR-035).
/// </summary>
public static partial class GatewayLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Gateway downstream base URL is not configured for {Service}.")]
    public static partial void DownstreamBaseUrlNotConfigured(
        this ILogger logger, DownstreamService service);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Gateway transport failure reaching {Service}.")]
    public static partial void TransportFailure(
        this ILogger logger, Exception exception, DownstreamService service);
}
