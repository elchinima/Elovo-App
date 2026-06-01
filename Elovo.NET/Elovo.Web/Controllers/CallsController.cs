using Elovo.Domain.Interfaces;

namespace Elovo.Web.Controllers;

[ApiController]
public class CallsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ICallHistoryService _callHistoryService;

    public CallsController(IUnitOfWork unitOfWork, IHubContext<ChatHub> hubContext, ICallHistoryService callHistoryService)
    {
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
        _callHistoryService = callHistoryService;
    }

    [AllowAnonymous]
    [HttpPost("/api/calls/reject")]
    public async Task<IActionResult> RejectCall([FromBody] RejectCallRequest request, CancellationToken cancellationToken)
    {
        var activeCall = await _unitOfWork.ActiveCalls.GetByCallerIdAsync(request.CallerId, cancellationToken);
        if (activeCall is null)
        {
            return NoContent();
        }

        activeCall.IsRejected = true;
        var message = await _callHistoryService.CompleteAsync(activeCall, CallStatuses.Rejected, cancellationToken);

        await _hubContext.Clients.Group($"user:{request.CallerId}").SendAsync("CallRejected");
        if (message is not null)
        {
            await _hubContext.Clients.Groups(new[] { $"user:{message.SenderId}", $"user:{message.ReceiverId}" })
                .SendAsync("ReceiveMessage", message, cancellationToken);
        }

        return NoContent();
    }
}

public sealed class RejectCallRequest
{
    public Guid CallerId { get; set; }
}
