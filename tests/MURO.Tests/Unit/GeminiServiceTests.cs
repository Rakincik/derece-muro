using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MURO.Infrastructure.Services;
using System.Net;
using System.Text;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// GeminiService için unit testler — API çağrısı, başarılı script üretimi,
/// hata durumunda fallback (ham metin), API key eksikliği.
/// </summary>
public class GeminiServiceTests
{
    private readonly Mock<ILogger<GeminiService>> _loggerMock = new();

    private GeminiService CreateService(HttpClient httpClient, string apiKey = "test-key")
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Gemini:ApiKey"]).Returns(apiKey);
        return new GeminiService(httpClient, configMock.Object, _loggerMock.Object);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode status, string responseBody)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    // ────────────────────────────────────────────────────────────────────────
    // HAPPY PATH
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnhanceToScript_Success_ShouldReturnScript()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{ "text": "Profesyonel podcast script içeriği..." }]
                }
            }]
        }
        """;

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = CreateService(httpClient);

        var result = await service.EnhanceToScriptAsync("Ham metin");
        result.Should().Be("Profesyonel podcast script içeriği...");
    }

    // ────────────────────────────────────────────────────────────────────────
    // FALLBACK — API hatası
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnhanceToScript_ApiFails_ShouldFallbackToRawText()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "error");
        var service = CreateService(httpClient);

        var result = await service.EnhanceToScriptAsync("Geri dönüş metni");
        result.Should().Be("Geri dönüş metni");
    }

    [Fact]
    public async Task EnhanceToScript_InvalidJson_ShouldFallbackToRawText()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "invalid json definitely");
        var service = CreateService(httpClient);

        var result = await service.EnhanceToScriptAsync("Ham metin geri dönüş");
        result.Should().Be("Ham metin geri dönüş");
    }

    [Fact]
    public async Task EnhanceToScript_Timeout_ShouldFallbackToRawText()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var httpClient = new HttpClient(handlerMock.Object);
        var service = CreateService(httpClient);

        var result = await service.EnhanceToScriptAsync("Timeout test");
        result.Should().Be("Timeout test");
    }

    // ────────────────────────────────────────────────────────────────────────
    // CONSTRUCTOR VALIDATION
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NoApiKey_ShouldThrow()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Gemini:ApiKey"]).Returns((string?)null);

        var act = () => new GeminiService(new HttpClient(), configMock.Object, _loggerMock.Object);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Gemini:ApiKey yapılandırılmamış*");
    }

    // ────────────────────────────────────────────────────────────────────────
    // LOGGING
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnhanceToScript_Success_ShouldLogInfo()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{ "text": "Out" }]
                }
            }]
        }
        """;

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = CreateService(httpClient);

        await service.EnhanceToScriptAsync("In");

        _loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
