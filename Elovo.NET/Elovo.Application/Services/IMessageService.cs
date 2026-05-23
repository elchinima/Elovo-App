
namespace Elovo.Application.Services;

public interface IMessageService
{
    Task<MessageDto> SendMessageAsync(Guid senderId, SendMessageDto dto, CancellationToken cancellationToken = default);
}
