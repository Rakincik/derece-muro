using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Google Gemini 1.5 Flash API ile metni profesyonel podcast script'e çevirir.
/// Ücretsiz tier: 15 RPM, 1M token/gün.
/// </summary>
public class GeminiService : IGeminiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;

    private const string ApiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    private const string SystemPrompt = """
        Sen, eğitim platformu için profesyonel Türkçe podcast içeriği üretiyor bir AI asistansın.
        
        Sana verilen ham metni şu kurallara göre dönüştür:
        - Doğal, akıcı ve ilgi çekici bir podcast monolog formatında yeniden yaz
        - Metni 3 bölüme ayır: Giriş (konu tanıtımı), Ana İçerik (detaylı anlatım), Kapanış (özet ve teşvik)
        - Teknik terimleri sadeleştir, örnekler ve benzetmeler ekle
        - Dinleyiciye (öğrencilere) hitap eden, sıcak ve motive edici bir dil kullan
        - Uzunluk: yaklaşık 300-500 kelime
        - Sadece konuşulacak metni yaz, başlık veya açıklama ekleme
        - Türkçe yaz
        """;

    public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> logger)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey yapılandırılmamış.");
        _logger = logger;
    }

    public async Task<string> EnhanceToScriptAsync(string rawText)
    {
        var url = $"{ApiUrl}?key={_apiKey}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = $"{SystemPrompt}\n\nHam Metin:\n{rawText}" }
                    }
                }
            },
            generationConfig = new
            {
                temperature       = 0.7,
                maxOutputTokens   = 1024,
                topP              = 0.9
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(url, body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var text = doc
                .RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? rawText;

            _logger.LogInformation("Gemini script üretimi tamamlandı ({Chars} karakter)", text.Length);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API hatası — ham metin olduğu gibi kullanılacak");
            return rawText; // Fallback: ham metni kullan
        }
    }
}
