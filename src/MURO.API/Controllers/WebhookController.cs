using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Webhooks;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

/// <summary>
/// Satış sitesinden gelen webhook'ları işler.
/// Güvenlik: Her istek X-Webhook-Signature header'ı ile HMAC-SHA256 imzalanmış olmalı.
/// </summary>
[EnableRateLimiting(RateLimitingConfig.GlobalPolicy)]
[ApiController]
[Route("api/v1/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookHandlerService _webhookHandler;
    private readonly IConfiguration _config;

    public WebhookController(IWebhookHandlerService webhookHandler, IConfiguration config)
    {
        _webhookHandler = webhookHandler;
        _config = config;
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseWebhookRequest request)
    {
        var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
        if (!VerifySignature(request, signature))
            return Unauthorized(new { error = "Geçersiz webhook imzası." });

        try
        {
            var result = await _webhookHandler.HandlePurchaseAsync(request);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelWebhookRequest request)
    {
        var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
        if (!VerifySignature(request, signature))
            return Unauthorized(new { error = "Geçersiz webhook imzası." });

        try
        {
            var result = await _webhookHandler.HandleCancelAsync(request);
            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private bool VerifySignature(object payload, string? signature)
    {
        if (string.IsNullOrEmpty(signature)) return false;

        var secret = _config["Webhook:Secret"];
        if (string.IsNullOrEmpty(secret)) return false; // Secret yoksa bypass (dev mode)

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return signature.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
