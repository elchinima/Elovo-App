
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddAutoMapper(_ => { }, typeof(ElovoMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();
builder.Services.AddInfrastructure(config);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IUserService, UserService>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!)),
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
}
finally
{
    scope.Dispose();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/auth/login"));
app.MapHub<ChatHub>("/chatHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();

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
