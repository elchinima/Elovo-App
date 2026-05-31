namespace Elovo.Domain;

public class PendingMessage
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsVoice { get; set; }
    public string? VoiceUrl { get; set; }
    public bool IsNotificationSent { get; set; }
    public bool IsCall { get; set; }
    public string? CallStatus { get; set; }
    public double? CallDurationSeconds { get; set; }
}
