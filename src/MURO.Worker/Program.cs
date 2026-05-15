using Microsoft.EntityFrameworkCore;
using Serilog;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using MURO.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// --- Serilog ---
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// --- Database ---
builder.Services.AddDbContext<MuroDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- HTTP Client (BBB indirme için) ---
builder.Services.AddHttpClient("bbb-download", client =>
{
    client.Timeout = TimeSpan.FromMinutes(60); // Büyük dosyalar için uzun timeout
});
builder.Services.AddHttpClient<BbbService>();

// --- Services ---
builder.Services.AddScoped<IBbbService, BbbService>();
builder.Services.AddScoped<IHlsProcessingService, HlsProcessingService>();

// --- Background Jobs ---
builder.Services.AddHostedService<BbbRecordingSyncJob>();
builder.Services.AddHostedService<UploadProcessingJob>();

var host = builder.Build();

Log.Information("MURO Worker başlatıldı.");
host.Run();
