namespace Elovo.Web.Services;

public sealed class UnansweredCallTimeoutJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UnansweredCallTimeout = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<UnansweredCallTimeoutJob> _logger;

    public UnansweredCallTimeoutJob(
        IServiceScopeFactory scopeFactory,
        IHubContext<ChatHub> hubContext,
        ILogger<UnansweredCallTimeoutJob> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CompleteExpiredCallsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unanswered call timeout job failed.");
            }
        }
    }

    private async Task CompleteExpiredCallsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ElovoDbContext>();
        var callHistoryService = scope.ServiceProvider.GetRequiredService<ICallHistoryService>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var expiresBefore = DateTime.UtcNow - UnansweredCallTimeout;

        var expiredCallIds = await dbContext.ActiveCalls
            .AsNoTracking()
            .Where(call => call.AnsweredAt == null && call.StartedAt <= expiresBefore)
            .OrderBy(call => call.StartedAt)
            .Select(call => call.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var callId in expiredCallIds)
        {
            try
            {
                await CompleteExpiredCallAsync(
                    dbContext,
                    callHistoryService,
                    pushNotificationService,
                    callId,
                    expiresBefore,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                dbContext.ChangeTracker.Clear();
                _logger.LogWarning(ex, "Could not complete unanswered call {CallId}.", callId);
            }
        }
    }

    private async Task CompleteExpiredCallAsync(
        ElovoDbContext dbContext,
        ICallHistoryService callHistoryService,
        IPushNotificationService pushNotificationService,
        Guid callId,
        DateTime expiresBefore,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var activeCall = await dbContext.ActiveCalls.FirstOrDefaultAsync(
            call => call.Id == callId && call.AnsweredAt == null && call.StartedAt <= expiresBefore,
            cancellationToken);
        if (activeCall is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var callerId = activeCall.CallerId;
        var receiverId = activeCall.ReceiverId;
        var message = await callHistoryService.CompleteAsync(activeCall, CallStatuses.Missed, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (message is not null)
        {
            await _hubContext.Clients.Groups(UserGroup(message.SenderId), UserGroup(message.ReceiverId))
                .SendAsync("ReceiveMessage", message, cancellationToken);
        }

        await _hubContext.Clients.Group(UserGroup(callerId)).SendAsync("CallTimedOut", cancellationToken);
        await _hubContext.Clients.Group(UserGroup(receiverId)).SendAsync("CallCancelled", cancellationToken);

        var fcmToken = await dbContext.UserSessions
            .Where(session => session.UserId == receiverId)
            .Select(session => session.FcmToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(fcmToken))
        {
            try
            {
                await pushNotificationService.SendCallCancelPushAsync(fcmToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not cancel the incoming call push for receiver {ReceiverId}.", receiverId);
            }
        }
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";
}
