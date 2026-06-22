namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class VoiceMessagesController : ControllerBase
{
    private const long StandardMaxVoiceBytes = 1024 * 1024;
    private const long ExtendedMaxVoiceBytes = 3 * 1024 * 1024;
    private const string StoredVoiceExtension = ".mp3";
    private const string StoredVoiceContentType = "audio/mpeg";
    private static readonly IReadOnlyDictionary<string, string> RecorderInputExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = ".webm",
        ["audio/ogg"] = ".ogg",
        ["audio/mp4"] = ".m4a"
    };

    private readonly IImageStorageService _imageStorageService;
    private readonly IUserService _userService;

    public VoiceMessagesController(IImageStorageService imageStorageService, IUserService userService)
    {
        _imageStorageService = imageStorageService;
        _userService = userService;
    }

    [HttpPost("/api/messages/voice")]
    [RequestSizeLimit(ExtendedMaxVoiceBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ExtendedMaxVoiceBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? voice, CancellationToken cancellationToken)
    {
        if (voice is null || voice.Length == 0)
        {
            return BadRequest("Voice message is required.");
        }

        var profile = await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken);
        var maxVoiceBytes = profile.IsExtendedVoiceMessagesEnabled ? ExtendedMaxVoiceBytes : StandardMaxVoiceBytes;
        if (voice.Length > maxVoiceBytes)
        {
            return BadRequest(profile.IsExtendedVoiceMessagesEnabled
                ? "Voice message must be 3 MB or less."
                : "Voice message must be 1 MB or less.");
        }

        if (!TryGetRecorderInputExtension(voice.ContentType, out var inputExtension))
        {
            return BadRequest("Voice message format is unsupported.");
        }

        await using var converted = new MemoryStream();
        try
        {
            await ConvertVoiceToMp3Async(voice, inputExtension, converted, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Voice conversion is unavailable.");
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Voice message file is invalid.");
        }

        converted.Position = 0;
        var storagePath = $"voices/{GetCurrentUserId():N}/{Guid.NewGuid():N}{StoredVoiceExtension}";
        var upload = await _imageStorageService.UploadAsync(converted, storagePath, StoredVoiceContentType, cancellationToken);

        return Ok(new
        {
            path = upload.Path,
            url = upload.Url
        });
    }

    [HttpGet("/api/messages/voice/file")]
    public async Task<IActionResult> Download([FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !_imageStorageService.IsVoicePath(path))
        {
            return NotFound();
        }

        try
        {
            var voice = await _imageStorageService.DownloadAsync(path, cancellationToken);
            Response.Headers.CacheControl = "private, max-age=604800";
            return File(voice.Bytes, voice.ContentType);
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

    private static bool TryGetRecorderInputExtension(string? contentType, out string extension)
    {
        extension = string.Empty;
        var normalizedContentType = NormalizeContentType(contentType);
        if (string.IsNullOrWhiteSpace(normalizedContentType) ||
            !RecorderInputExtensions.TryGetValue(normalizedContentType, out var matchedExtension))
        {
            return false;
        }

        extension = matchedExtension;
        return true;
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }

    private static async Task ConvertVoiceToMp3Async(IFormFile voice, string inputExtension, Stream output, CancellationToken cancellationToken)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "elovo-voices");
        Directory.CreateDirectory(tempRoot);

        var id = Guid.NewGuid().ToString("N");
        var inputPath = Path.Combine(tempRoot, $"{id}{inputExtension}");
        var outputPath = Path.Combine(tempRoot, $"{id}.mp3");

        try
        {
            await using (var input = System.IO.File.Create(inputPath))
            {
                await voice.CopyToAsync(input, cancellationToken);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-vn");
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add("44100");
            startInfo.ArgumentList.Add("-b:a");
            startInfo.ArgumentList.Add("64k");
            startInfo.ArgumentList.Add(outputPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Voice conversion failed.");

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !System.IO.File.Exists(outputPath))
            {
                throw new InvalidOperationException($"Voice conversion failed. {error}");
            }

            await using var converted = System.IO.File.OpenRead(outputPath);
            await converted.CopyToAsync(output, cancellationToken);
        }
        finally
        {
            DeleteTempFile(inputPath);
            DeleteTempFile(outputPath);
        }
    }

    private static void DeleteTempFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // Temp cleanup is best-effort.
        }
    }
}
