using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;

namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class ImageMessagesController : ControllerBase
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoBytes = 50 * 1024 * 1024;
    private const long MaxRequestBytes = 50 * 1024 * 1024;
    private const int ImageQuality = 50;
    private const double ImageResizeRatio = 0.5;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpeg",
        ".jpg",
        ".gif",
        ".mp4"
    };

    private readonly IImageStorageService _imageStorageService;
    private readonly IUserService _userService;

    public ImageMessagesController(
        IImageStorageService imageStorageService,
        IUserService userService)
    {
        _imageStorageService = imageStorageService;
        _userService = userService;
    }

    [HttpPost("/api/messages/images")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var extension = Path.GetExtension(image.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest("Only PNG, JPEG, JPG, GIF images and MP4 videos are allowed.");
        }

        var isVideo = extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        if (isVideo)
        {
            if (image.Length > MaxVideoBytes)
            {
                return BadRequest("Video size must be 50 MB or less.");
            }

            var profile = await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken);
            if (!profile.IsPremium || !profile.IsVideoUploadsEnabled)
            {
                return BadRequest("Video uploads are not enabled.");
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"elovo-videos-{Guid.NewGuid():N}.mp4");
            int width = 0;
            int height = 0;
            double fps = 0;
            try
            {
                var dir = Path.GetDirectoryName(tempPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await using (var tempStream = System.IO.File.Create(tempPath))
                {
                    await image.CopyToAsync(tempStream, cancellationToken);
                }
                (width, height, fps) = await ProbeVideoAsync(tempPath, cancellationToken);
            }
            catch {}
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }
                catch {}
            }

            var videoToken = width > 0 && height > 0 ? $"_q{height}_fps{(int)Math.Round(fps)}" : string.Empty;
            var videoStoragePath = $"videos/{GetCurrentUserId():N}/{Guid.NewGuid():N}{videoToken}{extension.ToLowerInvariant()}";

            await using var input = image.OpenReadStream();
            var videoUpload = await _imageStorageService.UploadAsync(input, videoStoragePath, "video/mp4", cancellationToken);

            return Ok(new
            {
                path = videoUpload.Path,
                url = videoUpload.Url,
                fileName = image.FileName,
                isRaw = false
            });
        }

        if (image.Length > MaxImageBytes)
        {
            return BadRequest("Image size must be 10 MB or less.");
        }

        var useRawImage = await IsRawImageUploadEnabledAsync(cancellationToken);
        await using var processed = new MemoryStream();
        ImageProcessingResult processing;

        try
        {
            using var input = image.OpenReadStream();
            processing = useRawImage
                ? await SaveRawImageAsync(input, processed, cancellationToken)
                : await SaveCompressedImageAsync(input, extension, processed, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BadRequest("Image file is invalid.");
        }

        var fileName = GetTaggedImageFileName(image.FileName, processing.IsRaw);
        var rawToken = processing.IsRaw ? "_raw" : string.Empty;
        var megapixelToken = GetMegapixelPathToken(processing.MegapixelLabel);
        var storagePath = $"messages/{GetCurrentUserId():N}/{Guid.NewGuid():N}{rawToken}{megapixelToken}{extension.ToLowerInvariant()}";

        processed.Position = 0;
        var upload = await _imageStorageService.UploadAsync(processed, storagePath, GetContentType(extension), cancellationToken);

        return Ok(new
        {
            path = upload.Path,
            url = upload.Url,
            fileName,
            isRaw = processing.IsRaw
        });
    }

    [HttpGet("/api/messages/images/file")]
    [HttpGet("/api/messages/videos/file")]
    public async Task<IActionResult> Download([FromQuery] string path, CancellationToken cancellationToken)
    {
        var isImage = _imageStorageService.IsImagePath(path);
        var isVideo = _imageStorageService.IsVideoPath(path);

        if (string.IsNullOrWhiteSpace(path) || (!isImage && !isVideo))
        {
            return NotFound();
        }

        try
        {
            var media = await _imageStorageService.DownloadAsync(path, cancellationToken);
            Response.Headers.CacheControl = "private, max-age=604800";
            return File(media.Bytes, media.ContentType, isVideo ? "video.mp4" : "image.jpg");
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

        if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mp4";
        }

        return "image/jpeg";
    }

    private async Task<bool> IsRawImageUploadEnabledAsync(CancellationToken cancellationToken)
    {
        var profile = await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken);
        return profile.IsPremium && profile.IsRawImageUploadsEnabled;
    }

    private sealed record ImageProcessingResult(string MegapixelLabel, bool IsRaw);

    private static async Task<ImageProcessingResult> SaveRawImageAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        await input.CopyToAsync(output, cancellationToken);
        output.Position = 0;
        var imageInfo = await Image.IdentifyAsync(output, cancellationToken);
        if (imageInfo is null)
        {
            throw new InvalidOperationException("Image file is invalid.");
        }

        output.Position = 0;
        return new ImageProcessingResult(GetImageMegapixelLabel(imageInfo.Width, imageInfo.Height), IsRaw: true);
    }

    private static async Task<ImageProcessingResult> SaveCompressedImageAsync(Stream input, string extension, Stream output, CancellationToken cancellationToken)
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
        var megapixelLabel = GetImageMegapixelLabel(image.Width, image.Height);

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsJpegAsync(output, new JpegEncoder
            {
                Quality = ImageQuality
            }, cancellationToken);
            return new ImageProcessingResult(megapixelLabel, IsRaw: false);
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsPngAsync(output, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            }, cancellationToken);
            return new ImageProcessingResult(megapixelLabel, IsRaw: false);
        }

        await image.SaveAsGifAsync(output, cancellationToken);
        return new ImageProcessingResult(megapixelLabel, IsRaw: false);
    }

    private static string GetImageMegapixelLabel(int width, int height)
    {
        var megapixels = (int)Math.Floor((double)width * height / 1000000);

        if (megapixels < 12)
        {
            return string.Empty;
        }

        if (megapixels == 12)
        {
            return "12MP";
        }

        if (megapixels < 48)
        {
            return "12MP+";
        }

        if (megapixels == 48)
        {
            return "48MP";
        }

        if (megapixels < 50)
        {
            return "48MP+";
        }

        if (megapixels == 50)
        {
            return "50MP";
        }

        if (megapixels < 64)
        {
            return "50MP+";
        }

        if (megapixels == 64)
        {
            return "64MP";
        }

        if (megapixels < 100)
        {
            return "64MP+";
        }

        if (megapixels == 100)
        {
            return "100MP";
        }

        if (megapixels < 108)
        {
            return "100MP+";
        }

        if (megapixels == 108)
        {
            return "108MP";
        }

        if (megapixels < 200)
        {
            return "108MP+";
        }

        if (megapixels == 200)
        {
            return "200MP";
        }

        return "200MP+";
    }

    private static string GetMegapixelPathToken(string label)
    {
        return string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : $"_mp{label.Replace("MP", string.Empty, StringComparison.Ordinal).Replace("+", "plus", StringComparison.Ordinal)}";
    }

    private static string GetTaggedImageFileName(string fileName, bool isRaw)
    {
        var cleanFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(cleanFileName))
        {
            cleanFileName = "image";
        }

        var tag = isRaw ? "Raw" : "Compressed";
        var name = Path.GetFileNameWithoutExtension(cleanFileName);
        var extension = Path.GetExtension(cleanFileName);
        return string.IsNullOrWhiteSpace(extension)
            ? $"{name} ({tag})"
            : $"{name} ({tag}){extension}";
    }

    private static async Task<(int Width, int Height, double Fps)> ProbeVideoAsync(string videoPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return (0, 0, 0);
        }

        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        int width = 0;
        int height = 0;
        double fps = 0;

        var resMatch = Regex.Match(error, @"\b(\d{3,5})x(\d{3,5})\b");
        if (resMatch.Success)
        {
            int.TryParse(resMatch.Groups[1].Value, out width);
            int.TryParse(resMatch.Groups[2].Value, out height);
        }

        var fpsMatch = Regex.Match(error, @"\b(\d+(?:\.\d+)?)\s*fps\b");
        if (fpsMatch.Success)
        {
            double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out fps);
        }
        else
        {
            var tbrMatch = Regex.Match(error, @"\b(\d+(?:\.\d+)?)\s*tbr\b");
            if (tbrMatch.Success)
            {
                double.TryParse(tbrMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out fps);
            }
        }

        return (width, height, fps);
    }
}
