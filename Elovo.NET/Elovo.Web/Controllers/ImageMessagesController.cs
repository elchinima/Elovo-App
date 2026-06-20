using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class ImageMessagesController : ControllerBase
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const int ImageQuality = 50;
    private const double ImageResizeRatio = 0.5;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpeg",
        ".jpg",
        ".gif"
    };

    private readonly IImageStorageService _imageStorageService;

    public ImageMessagesController(IImageStorageService imageStorageService)
    {
        _imageStorageService = imageStorageService;
    }

    [HttpPost("/api/messages/images")]
    [RequestSizeLimit(MaxImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImageBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest("Image is required.");
        }

        if (image.Length > MaxImageBytes)
        {
            return BadRequest("Image size must be 10 MB or less.");
        }

        var extension = Path.GetExtension(image.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest("Only PNG, JPEG, JPG and GIF images are allowed.");
        }

        var fileName = Path.GetFileName(image.FileName);
        var storagePath = $"messages/{GetCurrentUserId():N}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        await using var compressed = new MemoryStream();

        try
        {
            using var input = image.OpenReadStream();
            await SaveCompressedImageAsync(input, extension, compressed, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Image file is invalid.");
        }

        compressed.Position = 0;
        var upload = await _imageStorageService.UploadAsync(compressed, storagePath, GetContentType(extension), cancellationToken);

        return Ok(new
        {
            path = upload.Path,
            url = upload.Url,
            fileName
        });
    }

    [HttpGet("/api/messages/images/file")]
    public async Task<IActionResult> Download([FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !_imageStorageService.IsImagePath(path))
        {
            return NotFound();
        }

        try
        {
            var image = await _imageStorageService.DownloadAsync(path, cancellationToken);
            Response.Headers.CacheControl = "private, max-age=604800";
            return File(image.Bytes, image.ContentType, "image.jpg");
        }
        catch (Exception)
        {
            return NotFound();
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Current user id is missing.");
    }

    private static string GetContentType(string extension)
    {
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "image/gif";
        }

        return "image/jpeg";
    }

    private static async Task SaveCompressedImageAsync(Stream input, string extension, Stream output, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);
        var width = Math.Max(1, (int)Math.Round(image.Width * ImageResizeRatio));
        var height = Math.Max(1, (int)Math.Round(image.Height * ImageResizeRatio));
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsJpegAsync(output, new JpegEncoder
            {
                Quality = ImageQuality
            }, cancellationToken);
            return;
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsPngAsync(output, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            }, cancellationToken);
            return;
        }

        await image.SaveAsGifAsync(output, cancellationToken);
    }
}
