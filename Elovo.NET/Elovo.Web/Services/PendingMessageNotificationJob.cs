namespace Elovo.Web.Services;

public sealed class PendingMessageNotificationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

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
                dbContext.UserSessions,
                message => message.ReceiverId,
                session => session.UserId,
                (message, session) => new { Message = message, ReceiverFcmToken = session.FcmToken })
            .Join(
                dbContext.Users,
                notification => notification.Message.ReceiverId,
                receiver => receiver.Id,
                (notification, receiver) => new
                {
                    notification.Message,
                    notification.ReceiverFcmToken,
                    ReceiverPreferredLanguage = receiver.PreferredLanguage
                })
            .Join(
                dbContext.Users,
                notification => notification.Message.SenderId,
                sender => sender.Id,
                (notification, sender) => new
                {
                    notification.Message,
                    notification.ReceiverFcmToken,
                    notification.ReceiverPreferredLanguage,
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
                ? PushLocalization.GetText("Voice message", notification.ReceiverPreferredLanguage)
                : GetMessageNotificationBody(notification.Message.Content, notification.ReceiverPreferredLanguage);

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

    private static string GetMessageNotificationBody(string content, string? language)
    {
        return IsFileContent(content)
            ? PushLocalization.GetText("Sent a file", language)
            : content;
    }

    private static bool IsFileContent(string content)
    {
        var trimmedContent = content.Trim();
        if (trimmedContent.StartsWith("messages/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileReference = Uri.TryCreate(trimmedContent, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : trimmedContent;
        var fileName = Path.GetFileName(fileReference);
        var extension = Path.GetExtension(fileName);

        return extension.Length > 1
            && fileName.Length > extension.Length
            && extension[1..].All(char.IsLetterOrDigit);
    }
}
