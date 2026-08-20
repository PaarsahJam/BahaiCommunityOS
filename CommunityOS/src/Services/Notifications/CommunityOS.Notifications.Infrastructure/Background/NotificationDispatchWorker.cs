using CommunityOS.Notifications.Application.Services;
using CommunityOS.Notifications.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Notifications.Infrastructure.Background;

/// <summary>
/// Polls queued notifications and drives dispatch (ADR-025). InApp recipients
/// are delivered directly (a readable inbox row); Email/SMS/Push fail closed
/// (<c>provider-not-configured</c>) until a provider integration exists.
/// Preferences are applied by <see cref="NotificationDispatchService"/> before
/// dispatch. Each dispatch publishes the domain events BEFORE the save so the
/// <c>NotificationDispatched</c> outbox row and the notification change commit
/// atomically (ADR-015). The poll loop is resilient: a failed batch never
/// kills the worker.
/// </summary>
public sealed class NotificationDispatchWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.DispatchWorkerError(ex, ex.Message);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchDueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();

        var due = await notifications.ListQueuedForDispatchAsync(BatchSize, DateTime.UtcNow, ct);
        foreach (var notification in due)
        {
            await dispatchService.DispatchAsync(notification, ct);
            await notifications.UpdateAsync(notification, ct);
        }
    }
}

internal static partial class NotificationDispatchWorkerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Notification dispatch worker iteration failed: {Message}")]
    public static partial void DispatchWorkerError(this ILogger logger, Exception exception, string message);
}