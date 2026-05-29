namespace Elovo.Application.DTOs;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsVoice { get; set; }
    public string? VoiceUrl { get; set; }
    public double? VoiceDurationSeconds { get; set; }
    public bool IsImage { get; set; }
    public string? ImagePath { get; set; }
    public string? ImageStoragePath { get; set; }
    public string? ImageFileName { get; set; }
    public bool IsPending { get; set; }
}
