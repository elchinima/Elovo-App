
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
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !dto.ImagePath.StartsWith("/uploads/chat-images/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Image path is invalid.");
        }

        return await CreateMessageAsync(senderId, dto, true, cancellationToken);
    }

    private async Task<MessageDto> CreateMessageAsync(Guid senderId, SendMessageDto dto, bool isImage, CancellationToken cancellationToken)
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
            Content = isImage ? dto.ImagePath! : dto.Content.Trim(),
            SentAt = sentAt,
            IsImage = isImage,
            ImagePath = isImage ? dto.ImagePath : null,
            ImageFileName = isImage ? dto.ImageFileName : null
        };
    }
}
