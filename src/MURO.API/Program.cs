using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MURO.API.Extensions;
using MURO.API.Hubs;
using MURO.API.Middleware;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using FluentValidation;
using Serilog;
using StackExchange.Redis;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .Enrich.WithProperty("Application", "MURO-API")
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/muro-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// --- Database (Master DB — tenant registry + default) ---
builder.Services.AddDbContext<MuroDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Tenant DB Context Factory (per-tenant DB resolution) ---

// --- JWT Authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MURO",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MURO",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
        // SignalR WebSocket bağlantısı için query string'den token oku
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/notifications") || path.StartsWithSegments("/hubs/admin")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// --- Services (DI) — tek satırda tüm business servisleri ---
builder.Services.AddMuroBusinessServices();

// --- VEP Control Plane Entegrasyonu ---
builder.Services.AddHttpClient<IVepWebhookService, VepWebhookService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- AI Podcast Pipeline ---
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<EdgeTtsClient>();
builder.Services.AddScoped<IGeminiService, GeminiService>();
builder.Services.AddScoped<EdgeTtsClient>();

// --- BBB Integration (sertifika bypass sadece Development'ta) ---
builder.Services.AddMuroBbbIntegration(builder.Environment.IsDevelopment());

// --- 🚀 Redis Distributed Cache ---
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

// --- Notification Channels ---
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("muro");
    });
builder.Services.AddScoped<INotificationPush, MURO.API.Services.SignalRNotificationPush>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ISmsSender, SmsPlaceholderSender>();

// --- Redis & Caching altyapısı (tek satır) ---
builder.Services.AddMuroCaching(redisConnection);

// --- 🚀 Output Caching (Redis tabanlı) ---
builder.Services.AddOutputCache(options =>
{
    // Tenant bazlı kursları 24 saat önbellekle, değiştiğinde TenantCourses tag'iyle patlatacağız.
    options.AddPolicy("TenantCourses", policy => policy.Tag("TenantCourses").Expire(TimeSpan.FromHours(24)));
}).AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = redisConnection;
});

// --- 🚀 Response Compression (Brotli + Gzip) ---
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/plain", "application/javascript" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.SmallestSize);

// --- 🚀 Response Caching ---
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 64 * 1024 * 1024; // 64MB max cache body
    options.UseCaseSensitivePaths = false;
});

var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwrootPath, "podcasts"));

// --- Arka Plan Servisleri (Hafif olanlar API'de kalabilir) ---
builder.Services.AddHostedService<PackageExpiryService>();
// Not: UploadProcessingJob, ExamScoringJob ve SoftDeleteCleanupJob artık MURO.Worker içerisinde bağımsız çalışmaktadır.

// --- BBB Kayıt İşleme (Tetikleyici) ---
builder.Services.AddScoped<IHlsProcessingService, HlsProcessingService>();
builder.Services.AddHttpClient("bbb-download");
// Not: BbbRecordingSyncJob artık MURO.Worker içerisinde bağımsız çalışmaktadır.

// --- 🏥 Health Checks (Sistem Sağlığı) ---
var rabbitMqConnection = builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "PostgreSQL")
    .AddRedis(redisConnection, name: "Redis");

// --- 🔐 Rate Limiting (sadece Production'da aktif) ---
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddMuroRateLimiting();
}

// --- 🔐 Request Size Limits + HTTP/2 ---
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null; // Sınırsız (Endpoint bazlı kontrol)
    options.Limits.MinRequestBodyDataRate = null; // Çoklu yavaş yüklemelerde timeout atmaması için
    options.ConfigureEndpointDefaults(lo =>
        lo.Protocols = HttpProtocols.Http1AndHttp2);
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
{
    o.SerializerOptions.MaxDepth = 32; // JSON depth limiti
});

// --- Controllers + FluentValidation + Swagger ---
builder.Services.AddControllers(options =>
{
    // FluentValidation global action filter
    options.Filters.Add<FluentValidationActionFilter>();
})
.AddJsonOptions(o =>
{
    // Enum'ları string olarak serialize et (MediaStatus.Ready → "Ready")
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddScoped<FluentValidationActionFilter>();
builder.Services.AddValidatorsFromAssemblyContaining<MURO.Application.Validators.LoginRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "MURO API",
        Version = "v1",
        Description = "MURO — Multi-Tenant Uzaktan Eğitim Platformu REST API.\n\n" +
                      "**Kimlik Doğrulama:** Tüm korumalı endpoint'ler `Authorization: Bearer {token}` header'ı gerektirir.\n\n" +
                      "**Hata Formatı:** Tüm hatalar `{ error, errorCode, statusCode, traceId, timestamp }` formatında döner.",
        Contact = new() { Name = "MURO Destek", Email = "destek@muro.com" }
    });
    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // XML dokümantasyon dosyasını Swagger'a bağla
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT token giriniz"
    });
    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// --- 🔐 CORS (Sertleştirilmiş) ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[]
{
    "http://localhost:3000",
    "http://localhost:3001",
    "http://localhost:3002",
    "http://192.168.1.10:3000",
    "http://192.168.1.10:3001",
    "http://192.168.1.10:3002"
};
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id", "X-Requested-With", "X-Correlation-Id")
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// --- Forwarded Headers (for Nginx/Docker Proxy IP resolution) ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// --- 👑 SuperAdmin Auto-Seed --- (migration sonrasında çalıştırılıyor, aşağıda)

// --- 🔐 Middleware Pipeline (Güvenlik Sıralı) ---

// 0. Forwarded Headers — IP adreslerini proxy'den (Docker/Nginx) çözümle
app.UseForwardedHeaders();

// 0.1 Response Compression — tüm response'lara Brotli/Gzip uygula
app.UseResponseCompression();

// 0.5 Correlation ID — her isteğe benzersiz takip numarası
app.UseMiddleware<CorrelationIdMiddleware>();

// 1. Güvenlik header'ları — her response'a eklenir
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Exception handler — stack trace sızıntısını engeller
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2.5 Request Timing — istek süresini logla (500ms+ WARN, 2000ms+ ERROR)
app.UseMiddleware<RequestTimingMiddleware>();

// 3. Rate Limiter — DDoS ve abuse koruması (sadece Production)
if (!app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
}

// 4. Serilog request logging — UserId,  IP ile zenginleştirilmiş
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.000}ms | User:{UserId} Tenant:{TenantId}";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "-");
        diagnosticContext.Set("TenantId", httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                                          ?? httpContext.User.FindFirst("tenantId")?.Value ?? "-");
        diagnosticContext.Set("ClientIp", httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                                          ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault()?[..Math.Min(httpContext.Request.Headers.UserAgent.FirstOrDefault()?.Length ?? 0, 80)] ?? "-");
    };
});

// 5. Input sanitization — XSS ve SQL injection engeli
app.UseMiddleware<InputSanitizationMiddleware>();

// 6. Swagger — SADECE development ortamında
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseResponseCaching();
app.UseOutputCache(); // Enterprise Caching Middleware
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".m3u8"] = "application/x-mpegURL";
contentTypeProvider.Mappings[".ts"] = "video/MP2T";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");

        if (ctx.File.Name.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            // Video parçaları kalıcıdır, 1 yıl tarayıcıda önbellekle
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
        }
        else if (ctx.File.Name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            // Playlist dinamik değişebilir (yeni kalite eklenebilir ), no-cache
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache");
        }
    }
}); // Varsayılan wwwroot (root path için, HLS desteği eklendi)

// Nginx sadece /api isteklerini proxy ettiği için, uploads klasörünü /api/uploads altında dışa açıyoruz
var uploadsPath = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/api/v1/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");

        if (ctx.File.Name.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
        }
        else if (ctx.File.Name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache");
        }
    }
});

// 7. 🔐 Cookie → Authorization header (httpOnly cookie desteği)
app.UseMiddleware<CookieAuthMiddleware>();

// 8. 🔐 IP Blacklist — şüpheli IP'leri otomatik engelle
app.UseMiddleware<IpBlacklistMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
// Oturum geçerliliği: sessionId DB'de hâlâ aktif mi? (tek cihaz politikası)
app.UseMiddleware<SessionValidationMiddleware>();

// Body buffering: BBB webhook controller body'yi 2 kez okuyor (checksum + deserialization)
// ⚠️ MapControllers()'dan ÖNCE olmalı
app.Use(async (ctx, next) =>
{
    ctx.Request.EnableBuffering();
    await next();
});

// --- TUS Protocol Middleware ---
var tusUploadsPath = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(tusUploadsPath)) Directory.CreateDirectory(tusUploadsPath);

app.UseTus(httpContext => new DefaultTusConfiguration
{
    Store = new TusDiskStore(tusUploadsPath),
    UrlPath = "/api/v1/upload/tus",
    Events = new Events
    {
        OnFileCompleteAsync = async eventContext =>
        {
            var file = await eventContext.GetFileAsync();
            var metadata = await file.GetMetadataAsync(eventContext.CancellationToken);
            
            string extension = ".mp4"; // Default
            if (metadata.TryGetValue("filename", out var filenameMeta))
            {
                extension = Path.GetExtension(filenameMeta.GetString(System.Text.Encoding.UTF8));
            }

            var originalPath = Path.Combine(tusUploadsPath, file.Id);
            var finalPath = Path.Combine(tusUploadsPath, file.Id + extension);

            // TUS dosyayı tamamladığında, uzantısıyla birlikte kopyalayıp asıl dosyayı siliyoruz (veya move yapıyoruz).
            if (File.Exists(originalPath))
            {
                File.Move(originalPath, finalPath, overwrite: true);
            }

            // Temizlik (isteğe bağlı .info dosyası)
            var infoPath = Path.Combine(tusUploadsPath, file.Id + ".info");
            if (File.Exists(infoPath))
            {
                File.Delete(infoPath);
            }
        }
    }
});

app.MapControllers();
app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            info = report.Entries.Select(e => new { service = e.Key, status = e.Value.Status.ToString() })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<AdminHub>("/hubs/admin");

// --- Auto-Migrate & Seed in Development (with retry for Docker startup) ---
// Production: sadece migration (seed yok)
// Development: migration + demo data seed
{
    const int maxRetries = 5;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }
            
            // SuperAdmin seed — her ortamda çalışır (migration sonrası)
            var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            // await SuperAdminSeeder.SeedAsync(db, seedLogger);
            
            if (app.Environment.IsDevelopment())
            {
                await MURO.Infrastructure.Seeds.DatabaseSeeder.SeedAsync(db);
                Log.Information("Database migration ve seeding tamamlandı.");
            }
            else
            {
                Log.Information("Database migration tamamlandı (Production — seed atlandı).");
            }
            break;
        }
        catch (Exception ex) when (
            ex is Npgsql.NpgsqlException ||
            ex is System.Net.Sockets.SocketException ||
            ex.InnerException is Npgsql.NpgsqlException ||
            ex.InnerException is System.Net.Sockets.SocketException)
        {
            if (attempt == maxRetries)
            {
                Log.Fatal(ex,
                    "PostgreSQL bağlantısı {MaxRetries} denemede kurulamadı. " +
                    "Docker'ın çalıştığından emin olun: docker compose -f docker/docker-compose.yml up -d",
                    maxRetries);
                throw;
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s, 8s, 16s
            Log.Warning(
                "PostgreSQL bağlantısı bekleniyor... (deneme {Attempt}/{Max}, {Delay}s sonra tekrar denenecek)",
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

Log.Information("MURO API başlatıldı — {Url}", app.Urls.FirstOrDefault());
app.Run();

// Integration testleri için (WebApplicationFactory<Program>)
public partial class Program { }
