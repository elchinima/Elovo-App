using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Elovo.Web.Controllers;

[Authorize]
[Route("settings")]
public class ProfileController : Controller
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const string EmailCodeSendFailureMessage = "Could not send the verification code. Try again later.";
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpeg",
        ".jpg"
    };

    private readonly IImageStorageService _imageStorageService;
    private readonly ILogger<ProfileController> _logger;
    private readonly IUserService _userService;

    public ProfileController(
        IUserService userService,
        IImageStorageService imageStorageService,
        ILogger<ProfileController> logger)
    {
        _userService = userService;
        _imageStorageService = imageStorageService;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken));
    }

    [HttpGet("chat")]
    public async Task<IActionResult> Chat(CancellationToken cancellationToken)
    {
        var profile = await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken);
        ViewBag.CurrentUserId = profile.Id;
        ViewBag.CurrentUserIsPremium = profile.IsPremium;
        return View();
    }

    [HttpGet("premium-actions")]
    public async Task<IActionResult> PremiumActions(CancellationToken cancellationToken)
    {
        var profile = await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken);
        if (!profile.IsPremium)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(profile);
    }

    [HttpGet("/profile")]
    public IActionResult LegacyProfile()
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/api/profile")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetProfileAsync(GetCurrentUserId(), cancellationToken));
    }

    [HttpPost("/api/profile/email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.UpdateEmailAsync(GetCurrentUserId(), dto.Email, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Profile email update failed");
            return StatusCode(StatusCodes.Status500InternalServerError, EmailCodeSendFailureMessage);
        }
    }

    [HttpPost("/api/profile/email/verification-code")]
    public async Task<IActionResult> SendEmailVerificationCode(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SendEmailConfirmationCodeAsync(GetCurrentUserId(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Profile email verification code send failed");
            return StatusCode(StatusCodes.Status500InternalServerError, EmailCodeSendFailureMessage);
        }
    }

    [HttpPost("/api/profile/email/verify")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.VerifyEmailAsync(GetCurrentUserId(), dto.Code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _userService.ChangePasswordAsync(GetCurrentUserId(), dto.CurrentPassword, dto.NewPassword, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/two-factor")]
    public async Task<IActionResult> SetTwoFactor([FromBody] TwoFactorSettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetTwoFactorEnabledAsync(GetCurrentUserId(), dto.Enabled, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/extended-voice-messages")]
    public async Task<IActionResult> SetExtendedVoiceMessages([FromBody] ExtendedVoiceMessagesSettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetExtendedVoiceMessagesEnabledAsync(GetCurrentUserId(), dto.Enabled, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/premium-badge")]
    public async Task<IActionResult> SetPremiumBadge([FromBody] PremiumBadgeVisibilitySettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetPremiumBadgeVisibleAsync(GetCurrentUserId(), dto.Enabled, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/raw-image-uploads")]
    public async Task<IActionResult> SetRawImageUploads([FromBody] RawImageUploadsSettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetRawImageUploadsEnabledAsync(GetCurrentUserId(), dto.Enabled, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/video-uploads")]
    public async Task<IActionResult> SetVideoUploads([FromBody] VideoUploadsSettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetVideoUploadsEnabledAsync(GetCurrentUserId(), dto.Enabled, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/activity-visibility")]
    public async Task<IActionResult> SetActivityVisibility([FromBody] ActivityVisibilitySettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetActivityVisibilityAsync(GetCurrentUserId(), dto.Visibility, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/profile/image")]
    [RequestSizeLimit(MaxImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImageBytes)]
    public async Task<IActionResult> UploadImage(
        [FromForm] IFormFile? image,
        [FromQuery] bool isPremiumImage = false,
        CancellationToken cancellationToken = default)
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
            return BadRequest("Only PNG, JPEG and JPG images are allowed.");
        }

        var userId = GetCurrentUserId();
        var profile = await _userService.GetProfileAsync(userId, cancellationToken);

        if (isPremiumImage && !profile.IsPremium)
        {
            return BadRequest("Premium status is required to upload high-quality profile images.");
        }

        var suffix = isPremiumImage ? "_512" : "";
        var storagePath = $"profiles/{userId:N}/{Guid.NewGuid():N}{suffix}.webp";

        MemoryStream? mainStream = null;
        MemoryStream? smallStream = null;
        try
        {
            await using var input = image.OpenReadStream();
            (mainStream, smallStream) = await ProcessProfileImageAsync(input, isPremiumImage, cancellationToken);
        }
        catch
        {
            mainStream?.Dispose();
            smallStream?.Dispose();
            return BadRequest("Image file is invalid.");
        }

        try
        {
            await _imageStorageService.UploadAsync(mainStream, storagePath, "image/webp", cancellationToken);
            var smallStoragePath = storagePath.Replace(".webp", "_small.webp");
            await _imageStorageService.UploadAsync(smallStream, smallStoragePath, "image/webp", cancellationToken);
        }
        finally
        {
            await mainStream.DisposeAsync();
            await smallStream.DisposeAsync();
        }

        var updatedProfile = await _userService.SetProfileImagePathAsync(userId, storagePath, cancellationToken);

        if (!string.IsNullOrWhiteSpace(profile.ProfileImagePath))
        {
            await _imageStorageService.DeleteAsync(profile.ProfileImagePath, cancellationToken);
            var oldSmallPath = profile.ProfileImagePath.Replace(".webp", "_small.webp");
            try
            {
                await _imageStorageService.DeleteAsync(oldSmallPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old small profile image at {Path}", oldSmallPath);
            }
        }

        return Ok(updatedProfile);
    }

    [HttpDelete("/api/profile/image")]
    public async Task<IActionResult> DeleteImage(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(profile.ProfileImagePath))
        {
            await _imageStorageService.DeleteAsync(profile.ProfileImagePath, cancellationToken);
            var smallPath = profile.ProfileImagePath.Replace(".webp", "_small.webp");
            try
            {
                await _imageStorageService.DeleteAsync(smallPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete small profile image at {Path}", smallPath);
            }
        }

        return Ok(await _userService.RemoveProfileImagePathAsync(userId, cancellationToken));
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Current user id is missing.");
    }

    private static async Task<(MemoryStream MainImage, MemoryStream SmallImage)> ProcessProfileImageAsync(
        Stream input,
        bool isPremiumImage,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);
        var side = Math.Min(image.Width, image.Height);
        var crop = new Rectangle((image.Width - side) / 2, (image.Height - side) / 2, side, side);
        var targetSize = isPremiumImage ? 512 : 256;

        image.Mutate(x => x
            .Crop(crop)
            .Resize(new ResizeOptions
            {
                Size = new Size(targetSize, targetSize),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));

        var mainStream = new MemoryStream();
        await image.SaveAsWebpAsync(mainStream, new WebpEncoder
        {
            Quality = 50
        }, cancellationToken);

        mainStream.Position = 0;

        using var compressedImage = await Image.LoadAsync(mainStream, cancellationToken);
        compressedImage.Mutate(x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(64, 64),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));

        var smallStream = new MemoryStream();
        await compressedImage.SaveAsWebpAsync(smallStream, new WebpEncoder
        {
            Quality = 100
        }, cancellationToken);

        mainStream.Position = 0;
        smallStream.Position = 0;

        return (mainStream, smallStream);
    }
}
