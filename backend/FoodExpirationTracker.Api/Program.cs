using FirebaseAdmin;
using FoodExpirationTracker.Api.Middleware;
using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.Services;
using FoodExpirationTracker.Infrastructure.AI;
using FoodExpirationTracker.Infrastructure.Notifications;
using FoodExpirationTracker.Infrastructure.Ocr;
using FoodExpirationTracker.Infrastructure.Persistence;
using FoodExpirationTracker.Infrastructure.Persistence.Repositories;
using FoodExpirationTracker.Infrastructure.Security;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/foodtracker-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Initialize Firebase Admin SDK
// In production, read from FIREBASE_SERVICE_ACCOUNT_JSON env var (raw JSON string)
// Locally, fall back to file path from appsettings
var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
if (!string.IsNullOrWhiteSpace(firebaseJson))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromJson(firebaseJson),
    });
    Log.Information("Firebase Admin SDK initialized from FIREBASE_SERVICE_ACCOUNT_JSON env var");
}
else
{
    var firebaseKeyPath = builder.Configuration["Firebase:ServiceAccountKeyPath"];
    if (!string.IsNullOrWhiteSpace(firebaseKeyPath) && File.Exists(firebaseKeyPath))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(firebaseKeyPath),
        });
        Log.Information("Firebase Admin SDK initialized from {Path}", firebaseKeyPath);
    }
    else
    {
        Log.Warning("Firebase service account not found. Push notifications will fall back to console.");
    }
}

// Database connection: prefer DATABASE_URL env var (Render provides this),
// fall back to ConnectionStrings:DefaultConnection for local dev
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;
if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    connectionString = ConvertDatabaseUrl(databaseUrl);
    Log.Information("Using DATABASE_URL for PostgreSQL connection");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing DATABASE_URL env var or ConnectionStrings:DefaultConnection.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT secret: prefer env var, fall back to dev default
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "super-secret-key-change-me-for-prod-32-chars";

// Use port 5001 by default to avoid macOS AirPlay conflict on 5000.
// Override via PORT, API_PORT env var, or ASPNETCORE_URLS.
var basePort = int.TryParse(
    Environment.GetEnvironmentVariable("PORT")
    ?? Environment.GetEnvironmentVariable("API_PORT"), out var envPort) ? envPort : 5001;

// Auto-increment port if already in use (only locally; on Render PORT is always available)
var port = basePort;
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    for (var attempt = 0; attempt < 10; attempt++)
    {
        try
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            break; // Port is available
        }
        catch (System.Net.Sockets.SocketException)
        {
            Console.WriteLine($"Port {port} is in use, trying {port + 1}...");
            port++;
        }
    }
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                // Allow Capacitor native app origin
                if (origin.Equals("capacitor://localhost", StringComparison.OrdinalIgnoreCase)
                    || origin.Equals("http://localhost", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Allow localhost, LAN IPs (192.168.x.x, 10.x.x.x, 172.16-31.x.x)
                if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (System.Net.IPAddress.TryParse(uri.Host, out var ip))
                {
                    var bytes = ip.GetAddressBytes();
                    // 192.168.x.x or 10.x.x.x or 172.16-31.x.x
                    return (bytes[0] == 192 && bytes[1] == 168)
                        || bytes[0] == 10
                        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
                }

                // Allow Render and Vercel deployment URLs
                if (uri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IProductRepository, EfProductRepository>();
builder.Services.AddScoped<INotificationRepository, EfNotificationRepository>();
builder.Services.AddScoped<IRecipeRepository, EfRecipeRepository>();
builder.Services.AddScoped<IOcrCorrectionRepository, EfOcrCorrectionRepository>();
builder.Services.AddScoped<IDeviceTokenRepository, EfDeviceTokenRepository>();

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService>(_ => new JwtTokenService(jwtSecret));

builder.Services.AddSingleton<IOcrService, RegexOcrService>();
builder.Services.AddHttpClient<IGeminiVisionService, GeminiVisionService>()
    .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, delay, attempt, _) =>
                Log.Warning("Gemini retry {Attempt} after {Delay}s: {Status}", attempt, delay.TotalSeconds, outcome.Result?.StatusCode)));
if (FirebaseApp.DefaultInstance is not null)
{
    builder.Services.AddScoped<IPushNotificationSender, FirebasePushNotificationSender>();
}
else
{
    builder.Services.AddSingleton<IPushNotificationSender, ConsolePushNotificationSender>();
}

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OcrIngestionService>();
builder.Services.AddScoped<OcrCorrectionService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<NotificationService>();

builder.Services.AddHostedService<DailyExpiryHostedService>();

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("FrontendDev");

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SimpleJwtMiddleware>();

app.MapControllers();

Console.WriteLine($"API server starting on port {port}");
app.Run();

// Convert Render's DATABASE_URL (postgres://user:pass@host:port/db) to Npgsql connection string
static string ConvertDatabaseUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');
    var username = userInfo[0];
    var password = userInfo.Length > 1 ? userInfo[1] : "";

    // Use SSL for external hosts (Render, Neon, etc.), skip for local/Docker
    var isExternal = host.Contains(".render.com", StringComparison.OrdinalIgnoreCase)
        || host.Contains(".neon.tech", StringComparison.OrdinalIgnoreCase)
        || host.Contains(".neon.cloud", StringComparison.OrdinalIgnoreCase);
    var sslSuffix = isExternal ? ";SSL Mode=Require;Trust Server Certificate=true" : "";

    return $"Host={host};Port={port};Database={database};Username={username};Password={password}{sslSuffix}";
}
