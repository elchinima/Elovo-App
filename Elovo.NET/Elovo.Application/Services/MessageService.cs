using Elovo.Application.DTOs;
using Elovo.Domain.Interfaces;

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
            Content = dto.Content.Trim(),
            SentAt = sentAt
        };
    }

}
