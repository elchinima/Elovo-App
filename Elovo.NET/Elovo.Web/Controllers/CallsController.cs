using Elovo.Domain.Interfaces;

namespace Elovo.Web.Controllers;

[ApiController]
public class CallsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<ChatHub> _hubContext;

    public CallsController(IUnitOfWork unitOfWork, IHubContext<ChatHub> hubContext)
    {
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.Group($"user:{request.CallerId}").SendAsync("CallRejected", cancellationToken);

        return NoContent();
    }
}

public sealed class RejectCallRequest
{
    public Guid CallerId { get; set; }
}
