using Microsoft.Extensions.Logging;

namespace Elovo.Web.Services;

public sealed class RenderKeepAliveService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly HttpClient _httpClient;
    private readonly ILogger<RenderKeepAliveService> _logger;
    private readonly Uri? _healthUri;

    public RenderKeepAliveService(HttpClient httpClient, IConfiguration configuration, ILogger<RenderKeepAliveService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _healthUri = BuildHealthUri(configuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_healthUri is null)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var response = await _httpClient.GetAsync(_healthUri, stoppingToken);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Render keep-alive request failed.");
            }
        }
    }

    private static Uri? BuildHealthUri(IConfiguration configuration)
    {
        var publicUrl = configuration["Render:ExternalUrl"] ??
            configuration["App:PublicUrl"] ??
            Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");

        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        return new Uri(baseUri, "/health");
    }
}
