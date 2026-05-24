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
        var storagePath = NormalizeStoragePath(path);
        if (!IsImagePath(storagePath))
        {
            return;
        }

        using var request = CreateRequest(HttpMethod.Delete, $"object/{Bucket}/{EscapePath(storagePath)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Supabase image delete failed: {(int)response.StatusCode} {response.ReasonPhrase}. {details}");
    }

    public bool IsImagePath(string path)
    {
        return NormalizeStoragePath(path).StartsWith("messages/", StringComparison.Ordinal);
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

    private string NormalizeStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        var publicPrefix = $"/storage/v1/object/public/{Bucket}/";
        var objectPrefix = $"/storage/v1/object/{Bucket}/";

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return NormalizeStoragePathFromUri(uri.AbsolutePath, publicPrefix, objectPrefix);
        }

        return trimmed.TrimStart('/');
    }

    private static string NormalizeStoragePathFromUri(string absolutePath, string publicPrefix, string objectPrefix)
    {
        if (absolutePath.StartsWith(publicPrefix, StringComparison.Ordinal))
        {
            return Uri.UnescapeDataString(absolutePath[publicPrefix.Length..]);
        }

        if (absolutePath.StartsWith(objectPrefix, StringComparison.Ordinal))
        {
            return Uri.UnescapeDataString(absolutePath[objectPrefix.Length..]);
        }

        return absolutePath.TrimStart('/');
    }
}
