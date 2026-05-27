namespace Elovo.Web.Services;

public sealed class PushNotificationService
{
    private readonly object _appLock = new();
    private FirebaseApp? _firebaseApp;

    public PushNotificationService()
    {
    }

    public async Task SendPushAsync(string fcmToken, string title, string body)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            return;
        }

        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification
            {
                Title = title,
                Body = body
            }
        };

        await FirebaseMessaging.GetMessaging(GetFirebaseApp()).SendAsync(message);
    }

    private FirebaseApp GetFirebaseApp()
    {
        if (_firebaseApp is not null)
        {
            return _firebaseApp;
        }

        lock (_appLock)
        {
            if (_firebaseApp is not null)
            {
                return _firebaseApp;
            }

            var json = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("FIREBASE_CREDENTIALS_JSON environment variable is not set");
            }

            var credential = GoogleCredential
                .FromJson(json)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

            _firebaseApp = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            });

            return _firebaseApp;
        }
    }
}
