namespace Elovo.Application.DTOs;

public class SendMessageDto
{
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? ImageFileName { get; set; }
}
