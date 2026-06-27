using System.Net;
using Microsoft.Extensions.Logging;

namespace Elovo.Web.Services;

public class SupabaseImageStorageService : IImageStorageService
{
    private const string DefaultBucket = "chat-images";
    private const string DefaultProfileImagesBucket = "profile-images";
    private const string DefaultVoiceMessagesBucket = "chat-voices";
    private const string DefaultVideoMessagesBucket = "chat-videos";
    private const int DefaultMediaFileSizeLimit = 10 * 1024 * 1024;
    private const int VoiceFileSizeLimit = 1024 * 1024;
    private const int VideoFileSizeLimit = 50 * 1024 * 1024;

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
            throw new InvalidOperationException("Supabase media upload failed.");
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
        if (storagePath.StartsWith("voices/", StringComparison.Ordinal))
        {
            return $"/api/messages/voice/file?path={Uri.EscapeDataString(storagePath)}";
        }

        if (storagePath.StartsWith("videos/", StringComparison.Ordinal))
        {
            return $"/api/messages/videos/file?path={Uri.EscapeDataString(storagePath)}";
        }

        if (storagePath.StartsWith("messages/", StringComparison.Ordinal))
        {
            return $"/api/messages/images/file?path={Uri.EscapeDataString(storagePath)}";
        }

        return $"{BaseUrl}/storage/v1/object/public/{GetBucketForPath(storagePath)}/{EscapePath(storagePath)}";
    }

    public async Task<ImageDownloadResultDto> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        var storagePath = NormalizeStoragePath(path);
        if (!IsImagePath(storagePath) && !IsVoicePath(storagePath) && !IsVideoPath(storagePath))
        {
            throw new InvalidOperationException("Storage path is invalid.");
        }

        var bucket = GetBucketForPath(storagePath);
        using var request = CreateRequest(HttpMethod.Get, $"object/{bucket}/{EscapePath(storagePath)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Supabase media download failed.");
        }

        return new ImageDownloadResultDto
        {
            Bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken),
            ContentType = response.Content.Headers.ContentType?.MediaType ?? GetContentType(storagePath)
        };
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
            _logger.LogWarning("Supabase media not found: {Path}. Response: {Details}", storagePath, details);
            return;
        }

        throw new InvalidOperationException($"Supabase media delete failed: {(int)response.StatusCode} {response.ReasonPhrase}. {details}");
    }

    public bool IsImagePath(string path)
    {
        var storagePath = NormalizeStoragePath(path);
        return storagePath.StartsWith("messages/", StringComparison.Ordinal) && IsImageExtension(storagePath);
    }

    public bool IsVoicePath(string path)
    {
        var storagePath = NormalizeStoragePath(path);
        return storagePath.StartsWith("voices/", StringComparison.Ordinal) && IsVoiceExtension(storagePath);
    }

    public bool IsVideoPath(string path)
    {
        var storagePath = NormalizeStoragePath(path);
        return storagePath.StartsWith("videos/", StringComparison.Ordinal) && IsVideoExtension(storagePath);
    }

    private bool IsManagedImagePath(string path)
    {
        var normalizedPath = NormalizeStoragePath(path);
        return normalizedPath.StartsWith("messages/", StringComparison.Ordinal) ||
            normalizedPath.StartsWith("profiles/", StringComparison.Ordinal) ||
            normalizedPath.StartsWith("voices/", StringComparison.Ordinal) ||
            normalizedPath.StartsWith("videos/", StringComparison.Ordinal);
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
            file_size_limit = GetFileSizeLimitForBucket(bucket),
            allowed_mime_types = GetAllowedMimeTypesForBucket(bucket)
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

    private string VoiceMessagesBucket => GetConfiguredBucket("Supabase:VoiceMessagesBucket", DefaultVoiceMessagesBucket);

    private string VideoMessagesBucket => GetConfiguredBucket("Supabase:VideoMessagesBucket", DefaultVideoMessagesBucket);

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
        if (storagePath.StartsWith("profiles/", StringComparison.Ordinal))
        {
            return ProfileImagesBucket;
        }

        if (storagePath.StartsWith("voices/", StringComparison.Ordinal))
        {
            return VoiceMessagesBucket;
        }

        if (storagePath.StartsWith("videos/", StringComparison.Ordinal))
        {
            return VideoMessagesBucket;
        }

        return Bucket;
    }

    private int GetFileSizeLimitForBucket(string bucket)
    {
        if (bucket.Equals(VoiceMessagesBucket, StringComparison.Ordinal))
        {
            return VoiceFileSizeLimit;
        }

        if (bucket.Equals(VideoMessagesBucket, StringComparison.Ordinal))
        {
            return VideoFileSizeLimit;
        }

        return DefaultMediaFileSizeLimit;
    }

    private string[] GetAllowedMimeTypesForBucket(string bucket)
    {
        if (bucket.Equals(VoiceMessagesBucket, StringComparison.Ordinal))
        {
            return
            [
                "audio/mpeg"
            ];
        }

        if (bucket.Equals(VideoMessagesBucket, StringComparison.Ordinal))
        {
            return
            [
                "video/mp4"
            ];
        }

        return
        [
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/webp"
        ];
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

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "image/gif";
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return "audio/mpeg";
        }

        if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mp4";
        }

        return "image/jpeg";
    }

    private static bool IsImageExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVoiceExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVideoExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
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
        return new[] { Bucket, ProfileImagesBucket, VoiceMessagesBucket, VideoMessagesBucket }
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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
