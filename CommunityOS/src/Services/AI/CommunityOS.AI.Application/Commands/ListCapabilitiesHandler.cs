using CommunityOS.AI.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.AI.Application.Commands;

internal sealed class ListCapabilitiesHandler(
    IAiModelGateway gateway,
    ILogger<ListCapabilitiesHandler> logger)
    : IRequestHandler<ListCapabilitiesQuery, ListCapabilitiesResult>
{
    public Task<ListCapabilitiesResult> Handle(
        ListCapabilitiesQuery request,
        CancellationToken ct)
    {
        logger.CapabilitiesListed(request.SubjectId);

        var capabilities = gateway.ListCapabilities();

        return Task.FromResult(new ListCapabilitiesResult
        {
            Capabilities = capabilities
        });
    }
}

internal static partial class ListCapabilitiesLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "AI capabilities listed: subject={SubjectId}")]
    public static partial void CapabilitiesListed(this ILogger logger, Guid subjectId);
}
