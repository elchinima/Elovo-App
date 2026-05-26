
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var jwtSecret = GetRequiredConfigurationValue(config, "Jwt:Secret");

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddAutoMapper(_ => { }, typeof(ElovoMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();
builder.Services.AddInfrastructure(config);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddOptions();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHttpClient<IImageStorageService, SupabaseImageStorageService>();
builder.Services.AddHttpClient<RenderKeepAliveService>();
builder.Services.AddHostedService<RenderKeepAliveService>();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("ElovoAuthToken", out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (!context.Response.HasStarted)
                {
                    context.HandleResponse();
                    context.Response.Redirect("/auth/login");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
const long bandwidthLimitBytesPerSecond = 1024 * 1024;

var scope = app.Services.CreateScope();
try
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ElovoDbContext>();
    dbContext.Database.Migrate();
    dbContext.Database.ExecuteSqlRaw("""
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "LastLoginIp" character varying(45);
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "RegistrationIp" character varying(45);
        """);
}
finally
{
    scope.Dispose();
}

app.Use(async (context, next) =>
{
    var originalRequestBody = context.Request.Body;
    var originalResponseBody = context.Response.Body;

    var throttledRequestBody = new BandwidthThrottledReadStream(originalRequestBody, bandwidthLimitBytesPerSecond);
    var throttledResponseBody = new BandwidthThrottledWriteStream(originalResponseBody, bandwidthLimitBytesPerSecond);

    context.Request.Body = throttledRequestBody;
    context.Response.Body = throttledResponseBody;

    try
    {
        await next();
    }
    finally
    {
        context.Request.Body = originalRequestBody;
        context.Response.Body = originalResponseBody;
        await throttledRequestBody.DisposeAsync();
        await throttledResponseBody.DisposeAsync();
    }
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/auth/login"));
app.MapGet("/health", () => Results.Text("ok", "text/plain"));
app.MapHub<ChatHub>("/chatHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();

static string GetRequiredConfigurationValue(IConfiguration config, string key)
{
    var value = config[key];
    return string.IsNullOrWhiteSpace(value) || value.StartsWith("Set via ", StringComparison.OrdinalIgnoreCase)
        ? throw new InvalidOperationException($"{key} is not configured.")
        : value;
}

internal sealed class BandwidthThrottledReadStream : Stream
{
    private readonly Stream _inner;
    private readonly BandwidthThrottler _throttler;

    public BandwidthThrottledReadStream(Stream inner, long bytesPerSecond)
    {
        _inner = inner;
        _throttler = new BandwidthThrottler(bytesPerSecond);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override async Task FlushAsync(CancellationToken cancellationToken) =>
        await _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await _inner.ReadAsync(buffer, cancellationToken);
        await _throttler.WaitAsync(bytesRead, cancellationToken);
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
    }

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class BandwidthThrottledWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly BandwidthThrottler _throttler;

    public BandwidthThrottledWriteStream(Stream inner, long bytesPerSecond)
    {
        _inner = inner;
        _throttler = new BandwidthThrottler(bytesPerSecond);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override async Task FlushAsync(CancellationToken cancellationToken) =>
        await _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _throttler.WaitAsync(buffer.Length, cancellationToken);
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
    }

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class BandwidthThrottler
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly long _bytesPerSecond;
    private long _transferredBytes;

    public BandwidthThrottler(long bytesPerSecond)
    {
        _bytesPerSecond = bytesPerSecond;
    }

    public async ValueTask WaitAsync(int bytes, CancellationToken cancellationToken)
    {
        if (bytes <= 0)
        {
            return;
        }

        _transferredBytes += bytes;
        var expectedTicks = _transferredBytes * Stopwatch.Frequency / _bytesPerSecond;
        var delayTicks = expectedTicks - _stopwatch.ElapsedTicks;

        if (delayTicks <= 0)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds((double)delayTicks / Stopwatch.Frequency);
        await Task.Delay(delay, cancellationToken);
    }
}
