namespace Elovo.Application.Services;

public interface IImageStorageService
{
    Task<ImageUploadResultDto> UploadAsync(Stream stream, string path, string contentType, CancellationToken cancellationToken = default);
    Task<ImageDownloadResultDto> DownloadAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
    string GetPublicUrl(string path);
    bool IsImagePath(string path);
    bool IsVoicePath(string path);
    bool IsVideoPath(string path);
}
