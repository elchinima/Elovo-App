namespace Elovo.Web.Services;

public class SupabaseImageStorageService : IImageStorageService
{
    private const string DefaultBucket = "chat-images";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public SupabaseImageStorageService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<ImageUploadResultDto> UploadAsync(Stream stream, string path, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        using var request = CreateRequest(HttpMethod.Post, $"object/{Bucket}/{EscapePath(path)}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Supabase image upload failed.");
        }

        return new ImageUploadResultDto
        {
            Path = path,
            Url = GetPublicUrl(path)
        };
    }

    public string GetPublicUrl(string path)
    {
        return $"{BaseUrl}/storage/v1/object/public/{Bucket}/{EscapePath(path)}";
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!IsImagePath(path))
        {
            return;
        }

        using var request = CreateRequest(HttpMethod.Delete, $"object/{Bucket}");
        request.Content = JsonContent.Create(new { prefixes = new[] { path } });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
    }

    public bool IsImagePath(string path)
    {
        return path.StartsWith("messages/", StringComparison.Ordinal);
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "bucket");
        request.Content = JsonContent.Create(new
        {
            id = Bucket,
            name = Bucket,
            @public = true,
            file_size_limit = 10 * 1024 * 1024,
            allowed_mime_types = new[] { "image/png", "image/jpeg", "image/gif" }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}/storage/v1/{path}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        request.Headers.TryAddWithoutValidation("apikey", ApiKey);
        return request;
    }

    private string BaseUrl => (_configuration["Supabase:Url"] ?? "https://stwmvvnzcgtagztttboy.supabase.co").TrimEnd('/');

    private string Bucket => string.IsNullOrWhiteSpace(_configuration["Supabase:StorageBucket"])
        ? DefaultBucket
        : _configuration["Supabase:StorageBucket"]!;

    private string ApiKey
    {
        get
        {
            var key = FirstConfigured("Supabase:ServiceRoleKey", "Supabase:StorageKey", "Supabase:AnonKey");
            return string.IsNullOrWhiteSpace(key)
                ? throw new InvalidOperationException("Supabase storage key is missing.")
                : key;
        }
    }

    private string? FirstConfigured(params string[] keys)
    {
        return keys
            .Select(key => _configuration[key])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string EscapePath(string path)
    {
        return string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }
}
