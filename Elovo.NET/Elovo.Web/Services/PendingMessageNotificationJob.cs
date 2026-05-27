namespace Elovo.Web.Services;

public sealed class PendingMessageNotificationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingMessageNotificationJob> _logger;

    public PendingMessageNotificationJob(IServiceScopeFactory scopeFactory, ILogger<PendingMessageNotificationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SendPendingNotificationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pending message notification job failed.");
            }
        }
    }

    private async Task SendPendingNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ElovoDbContext>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

        var pendingNotifications = await dbContext.PendingMessages
            .Where(message => !message.IsNotificationSent)
            .Join(
                dbContext.Users,
                message => message.ReceiverId,
                receiver => receiver.Id,
                (message, receiver) => new { Message = message, ReceiverFcmToken = receiver.FcmToken })
            .Join(
                dbContext.Users,
                notification => notification.Message.SenderId,
                sender => sender.Id,
                (notification, sender) => new
                {
                    notification.Message,
                    notification.ReceiverFcmToken,
                    SenderUsername = sender.Username
                })
            .Where(notification => notification.ReceiverFcmToken != null && notification.ReceiverFcmToken != string.Empty)
            .OrderBy(notification => notification.Message.SentAt)
            .ThenBy(notification => notification.Message.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var notification in pendingNotifications)
        {
            var body = notification.Message.IsVoice
                ? "Voice message"
                : notification.Message.Content;

            await pushNotificationService.SendPushAsync(
                notification.ReceiverFcmToken!,
                notification.SenderUsername,
                body);

            notification.Message.IsNotificationSent = true;
        }

        if (pendingNotifications.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
