using System.Security.Claims;
using Elovo.Application.DTOs;
using Elovo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class MessagesApiController : ControllerBase
{
    private readonly IUserService _userService;

    public MessagesApiController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("/api/conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var conversations = await _userService.GetConversationsAsync(GetCurrentUserId(), cancellationToken);
        return Ok(conversations);
    }

    [HttpGet("/api/users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var users = await _userService.SearchFriendCandidatesAsync(GetCurrentUserId(), query, cancellationToken);
        return Ok(users);
    }

    [HttpGet("/api/friend-requests")]
    public async Task<IActionResult> GetFriendRequests(CancellationToken cancellationToken)
    {
        var requests = await _userService.GetIncomingFriendRequestsAsync(GetCurrentUserId(), cancellationToken);
        return Ok(requests);
    }

    [HttpPost("/api/friend-requests")]
    public async Task<IActionResult> SendFriendRequest([FromBody] CreateFriendRequestDto dto, CancellationToken cancellationToken)
    {
        await _userService.SendFriendRequestAsync(GetCurrentUserId(), dto.ReceiverId, cancellationToken);
        return NoContent();
    }

    [HttpPost("/api/friend-requests/{requestId:guid}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(Guid requestId, CancellationToken cancellationToken)
    {
        await _userService.AcceptFriendRequestAsync(GetCurrentUserId(), requestId, cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Current user id is missing.");
    }
}
