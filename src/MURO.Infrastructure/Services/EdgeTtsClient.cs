using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Python edge-tts mikroservisine HTTP çağrısı yaparak MP3 üretir ve diske kaydeder.
/// </summary>
public class EdgeTtsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<EdgeTtsClient> _logger;
    private readonly string _baseUrl;

    public EdgeTtsClient(HttpClient http, IConfiguration config, ILogger<EdgeTtsClient> logger)
    {
        _http    = http;
        _logger  = logger;
        _baseUrl = config["EdgeTts:BaseUrl"] ?? "http://localhost:5050";
    }

    /// <summary>
    /// Script'i sese çevirir, wwwroot/podcasts/ klasörüne kaydeder.
    /// </summary>
    /// <returns>Kaydedilen dosyanın göreceli web yolu (örn. /podcasts/abc123.mp3)</returns>
    public async Task<(string RelativePath, int DurationSeconds)> SynthesizeAsync(
        string script, string voice, string wwwrootPath)
    {
        var body = new { script, voice };

        _logger.LogInformation("TTS sentez başlatılıyor — ses: {Voice}, uzunluk: {Len} karakter", voice, script.Length);

        var response = await _http.PostAsJsonAsync($"{_baseUrl}/synthesize", body);
        response.EnsureSuccessStatusCode();

        // Klasör yoksa oluştur
        var podcastDir = Path.Combine(wwwrootPath, "podcasts");
        Directory.CreateDirectory(podcastDir);

        var fileName     = $"{Guid.NewGuid():N}.mp3";
        var fullPath     = Path.Combine(podcastDir, fileName);
        var relativePath = $"/podcasts/{fileName}";

        await using var stream   = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(fullPath);
        await stream.CopyToAsync(fileStream);

        // Yaklaşık süre: ortalama 150 kelime/dakika TTS hızı
        var wordCount       = script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var durationSeconds = Math.Max(10, (int)(wordCount / 2.5));

        _logger.LogInformation("MP3 kaydedildi: {Path} (~{Sec}s)", fullPath, durationSeconds);
        return (relativePath, durationSeconds);
    }
}
