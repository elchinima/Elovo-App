using System.Net;
using Microsoft.Extensions.Logging;

namespace Elovo.Web.Services;

public class SupabaseImageStorageService : IImageStorageService
{
    private const string DefaultBucket = "chat-images";
    private const string DefaultProfileImagesBucket = "profile-images";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupabaseImageStorageService> _logger;

    public SupabaseImageStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseImageStorageService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ImageUploadResultDto> UploadAsync(Stream stream, string path, string contentType, CancellationToken cancellationToken = default)
    {
        var bucket = GetBucketForPath(path);
        await EnsureBucketAsync(bucket, cancellationToken);

        using var request = CreateRequest(HttpMethod.Post, $"object/{bucket}/{EscapePath(path)}");
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
        var storagePath = NormalizeStoragePath(path);
        return $"{BaseUrl}/storage/v1/object/public/{GetBucketForPath(storagePath)}/{EscapePath(storagePath)}";
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var storagePath = NormalizeStoragePath(path);
        if (!IsManagedImagePath(storagePath))
        {
            return;
        }

        var bucket = GetBucketForPath(storagePath);
        using var request = CreateRequest(HttpMethod.Delete, $"object/{bucket}/{EscapePath(storagePath)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || IsNotFoundResponse(details))
        {
            _logger.LogWarning("Supabase image not found: {Path}. Response: {Details}", storagePath, details);
            return;
        }

        throw new InvalidOperationException($"Supabase image delete failed: {(int)response.StatusCode} {response.ReasonPhrase}. {details}");
    }

    public bool IsImagePath(string path)
    {
        return NormalizeStoragePath(path).StartsWith("messages/", StringComparison.Ordinal);
    }

    private bool IsManagedImagePath(string path)
    {
        var normalizedPath = NormalizeStoragePath(path);
        return normalizedPath.StartsWith("messages/", StringComparison.Ordinal) ||
            normalizedPath.StartsWith("profiles/", StringComparison.Ordinal);
    }

    private async Task EnsureBucketAsync(string bucket, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "bucket");
        request.Content = JsonContent.Create(CreateBucketOptions(bucket));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            using var updateRequest = CreateRequest(HttpMethod.Put, $"bucket/{Uri.EscapeDataString(bucket)}");
            updateRequest.Content = JsonContent.Create(CreateBucketOptions(bucket));
            using var updateResponse = await _httpClient.SendAsync(updateRequest, cancellationToken);
            if (updateResponse.IsSuccessStatusCode)
            {
                return;
            }
        }
    }

    private object CreateBucketOptions(string bucket)
    {
        return new
        {
            id = bucket,
            name = bucket,
            @public = true,
            file_size_limit = 10 * 1024 * 1024,
            allowed_mime_types = new[] { "image/png", "image/jpeg", "image/gif", "image/webp" }
        };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}/storage/v1/{path}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        request.Headers.TryAddWithoutValidation("apikey", ApiKey);
        return request;
    }

    private string BaseUrl
    {
        get
        {
            var url = _configuration["Supabase:Url"];
            return string.IsNullOrWhiteSpace(url) || IsPlaceholder(url)
                ? throw new InvalidOperationException("Supabase:Url is not configured.")
                : url.TrimEnd('/');
        }
    }

    private string Bucket => GetConfiguredBucket("Supabase:StorageBucket", DefaultBucket);

    private string ProfileImagesBucket => GetConfiguredBucket("Supabase:ProfileImagesBucket", DefaultProfileImagesBucket);

    private string GetConfiguredBucket(string key, string fallback)
    {
        var value = _configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        _logger.LogInformation("Supabase bucket setting {Key} is missing. Using fallback bucket {Bucket}.", key, fallback);
        return fallback;
    }

    private string GetBucketForPath(string path)
    {
        var storagePath = NormalizeStoragePath(path);
        return storagePath.StartsWith("profiles/", StringComparison.Ordinal)
            ? ProfileImagesBucket
            : Bucket;
    }

    private string ApiKey
    {
        get
        {
            var key = FirstConfigured("Supabase:ServiceRoleKey", "Supabase:StorageKey", "Supabase:AnonKey");
            return string.IsNullOrWhiteSpace(key) || IsPlaceholder(key)
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

    private static bool IsNotFoundResponse(string details)
    {
        return details.Contains("not_found", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("Object not found", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholder(string value)
    {
        return value.StartsWith("Set via ", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return NormalizeStoragePathFromUri(uri.AbsolutePath, GetKnownBuckets());
        }

        return trimmed.TrimStart('/');
    }

    private IReadOnlyList<string> GetKnownBuckets()
    {
        return Bucket.Equals(ProfileImagesBucket, StringComparison.Ordinal)
            ? [Bucket]
            : [Bucket, ProfileImagesBucket];
    }

    private static string NormalizeStoragePathFromUri(string absolutePath, IReadOnlyList<string> buckets)
    {
        foreach (var bucket in buckets)
        {
            var publicPrefix = $"/storage/v1/object/public/{bucket}/";
            var objectPrefix = $"/storage/v1/object/{bucket}/";

            if (absolutePath.StartsWith(publicPrefix, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(absolutePath[publicPrefix.Length..]);
            }

            if (absolutePath.StartsWith(objectPrefix, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(absolutePath[objectPrefix.Length..]);
            }
        }

        return absolutePath.TrimStart('/');
    }
}
