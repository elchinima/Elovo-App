namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class ImageMessagesController : ControllerBase
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpeg",
        ".jpg",
        ".gif"
    };

    private readonly IWebHostEnvironment _environment;

    public ImageMessagesController(IWebHostEnvironment environment)
    {
        _environment = environment;
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

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "chat-images");
        Directory.CreateDirectory(uploadRoot);
        var physicalPath = Path.Combine(uploadRoot, fileName);

        try
        {
            await using var output = System.IO.File.Create(physicalPath);
            using var input = image.OpenReadStream();
            await SaveCompressedImageAsync(input, extension, output, cancellationToken);
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            return BadRequest("Image file is invalid.");
        }

        return Ok(new
        {
            path = $"/uploads/chat-images/{fileName}",
            fileName = Path.GetFileName(image.FileName)
        });
    }

    private static async Task SaveCompressedImageAsync(Stream input, string extension, Stream output, CancellationToken cancellationToken)
    {
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(input, cancellationToken);

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsJpegAsync(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = 50
            }, cancellationToken);
            return;
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsPngAsync(output, new SixLabors.ImageSharp.Formats.Png.PngEncoder
            {
                CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression
            }, cancellationToken);
            return;
        }

        await image.SaveAsGifAsync(output, cancellationToken);
    }
}
