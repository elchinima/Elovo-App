namespace Elovo.Application.Services;

public interface IPushNotificationService
{
    Task SendCallPushAsync(string fcmToken, string callerName, string callerAvatar, string callerId);
}
