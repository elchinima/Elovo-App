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
        var imageType = Type.GetType("SixLabors.ImageSharp.Image, SixLabors.ImageSharp");
        var extensionType = Type.GetType("SixLabors.ImageSharp.ImageExtensions, SixLabors.ImageSharp");

        if (imageType is null || extensionType is null)
        {
            await input.CopyToAsync(output, cancellationToken);
            return;
        }

        var loadMethod = imageType.GetMethods()
            .FirstOrDefault(x =>
            {
                var parameters = x.GetParameters();
                return x.Name == "LoadAsync" &&
                    parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(Stream) &&
                    parameters[1].ParameterType == typeof(CancellationToken);
            }) ?? throw new InvalidOperationException("ImageSharp loader is unavailable.");

        var imageTask = (Task?)loadMethod.Invoke(null, new object[] { input, cancellationToken })
            ?? throw new InvalidOperationException("ImageSharp loader failed.");

        await imageTask;
        var image = imageTask.GetType().GetProperty("Result")?.GetValue(imageTask)
            ?? throw new InvalidOperationException("Image file is invalid.");

        try
        {
            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                await SaveWithEncoderAsync(extensionType, image, output, "SaveAsJpegAsync", "SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder, SixLabors.ImageSharp", cancellationToken, encoder =>
                {
                    encoder.GetType().GetProperty("Quality")?.SetValue(encoder, 50);
                });
                return;
            }

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                await SaveWithEncoderAsync(extensionType, image, output, "SaveAsPngAsync", "SixLabors.ImageSharp.Formats.Png.PngEncoder, SixLabors.ImageSharp", cancellationToken, encoder =>
                {
                    var property = encoder.GetType().GetProperty("CompressionLevel");
                    if (property?.PropertyType.IsEnum == true && Enum.TryParse(property.PropertyType, "BestCompression", out var value))
                    {
                        property.SetValue(encoder, value);
                    }
                });
                return;
            }

            var gifMethod = extensionType.GetMethods()
                .FirstOrDefault(x =>
                {
                    var parameters = x.GetParameters();
                    return x.Name == "SaveAsGifAsync" &&
                        parameters.Length == 3 &&
                        parameters[1].ParameterType == typeof(Stream) &&
                        parameters[2].ParameterType == typeof(CancellationToken);
                }) ?? throw new InvalidOperationException("ImageSharp GIF encoder is unavailable.");

            var gifTask = (Task?)gifMethod.Invoke(null, new[] { image, output, cancellationToken })
                ?? throw new InvalidOperationException("ImageSharp GIF encoder failed.");
            await gifTask;
        }
        finally
        {
            if (image is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static async Task SaveWithEncoderAsync(Type extensionType, object image, Stream output, string methodName, string encoderTypeName, CancellationToken cancellationToken, Action<object> configure)
    {
        var encoderType = Type.GetType(encoderTypeName)
            ?? throw new InvalidOperationException("ImageSharp encoder is unavailable.");
        var encoder = Activator.CreateInstance(encoderType)
            ?? throw new InvalidOperationException("ImageSharp encoder failed.");
        configure(encoder);

        var method = extensionType.GetMethods()
            .FirstOrDefault(x =>
            {
                var parameters = x.GetParameters();
                return x.Name == methodName &&
                    parameters.Length == 4 &&
                    parameters[1].ParameterType == typeof(Stream) &&
                    parameters[2].ParameterType == encoderType &&
                    parameters[3].ParameterType == typeof(CancellationToken);
            }) ?? throw new InvalidOperationException("ImageSharp encoder is unavailable.");

        var saveTask = (Task?)method.Invoke(null, new[] { image, output, encoder, cancellationToken })
            ?? throw new InvalidOperationException("ImageSharp encoder failed.");
        await saveTask;
    }
}
