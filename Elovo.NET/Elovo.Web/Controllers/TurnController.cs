namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class TurnController : ControllerBase
{
    private const int TurnCredentialsTtlSeconds = 86400;
    private readonly IHttpClientFactory _httpClientFactory;

    public TurnController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("/api/turn-credentials")]
    public async Task<IActionResult> GetCredentials(CancellationToken cancellationToken)
    {
        var keyId = Environment.GetEnvironmentVariable("CLOUDFLARE_TURN_KEY_ID");
        var apiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_TURN_API_TOKEN");

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(apiToken))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "TURN credentials are not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://rtc.live.cloudflare.com/v1/turn/keys/{Uri.EscapeDataString(keyId)}/credentials/generate-ice-servers");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = JsonContent.Create(new { ttl = TurnCredentialsTtlSeconds });

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(StatusCodes.Status502BadGateway, payload);
        }

        if (!payload.TryGetProperty("iceServers", out var iceServers) || iceServers.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Cloudflare TURN response did not include iceServers.");
        }

        return Ok(new { iceServers });
    }
}
