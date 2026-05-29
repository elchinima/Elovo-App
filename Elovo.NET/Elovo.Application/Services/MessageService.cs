
namespace Elovo.Application.Services;

public class MessageService : IMessageService
{
    private readonly IUnitOfWork _unitOfWork;

    public MessageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<MessageDto> SendMessageAsync(Guid senderId, SendMessageDto dto, CancellationToken cancellationToken = default)
    {
        return await CreateMessageAsync(senderId, dto, false, cancellationToken);
    }

    public async Task<MessageDto> SendImageMessageAsync(Guid senderId, SendMessageDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ImagePath))
        {
            throw new InvalidOperationException("Image path is invalid.");
        }

        return await CreateMessageAsync(senderId, dto, true, cancellationToken);
    }

    public async Task<MessageDto> SendVoiceMessageAsync(Guid senderId, SendMessageDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.VoicePath))
        {
            throw new InvalidOperationException("Voice path is invalid.");
        }

        return await CreateMessageAsync(senderId, dto, isImage: false, isVoice: true, cancellationToken);
    }

    private async Task<MessageDto> CreateMessageAsync(Guid senderId, SendMessageDto dto, bool isImage, CancellationToken cancellationToken)
    {
        return await CreateMessageAsync(senderId, dto, isImage, isVoice: false, cancellationToken);
    }

    private async Task<MessageDto> CreateMessageAsync(Guid senderId, SendMessageDto dto, bool isImage, bool isVoice, CancellationToken cancellationToken)
    {
        var receiver = await _unitOfWork.Users.GetByIdAsync(dto.ReceiverId, cancellationToken)
            ?? throw new InvalidOperationException("Receiver was not found.");

        var conversation = await _unitOfWork.Conversations.GetBetweenUsersAsync(senderId, receiver.Id, cancellationToken);
        if (conversation is null)
        {
            throw new InvalidOperationException("You can message only friends.");
        }

        var sentAt = DateTime.UtcNow;
        conversation.UpdatedAt = sentAt;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MessageDto
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = senderId,
            ReceiverId = receiver.Id,
            Content = isVoice ? "Voice message" : isImage ? dto.ImagePath! : dto.Content.Trim(),
            SentAt = sentAt,
            IsVoice = isVoice,
            VoiceUrl = isVoice ? dto.VoicePath : null,
            VoiceDurationSeconds = isVoice ? dto.VoiceDurationSeconds : null,
            IsImage = isImage,
            ImagePath = isImage ? dto.ImagePath : null,
            ImageStoragePath = isImage ? dto.ImagePath : null,
            ImageFileName = isImage ? dto.ImageFileName : null
        };
    }
}
