namespace Elovo.Application.Services;

public interface IImageStorageService
{
    Task<ImageUploadResultDto> UploadAsync(Stream stream, string path, string contentType, CancellationToken cancellationToken = default);
    string GetPublicUrl(string path);
    bool IsImagePath(string path);
}
