using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MURO.Application.DTOs.Support;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using System.Text.Json;

namespace MURO.API.Controllers;

[ApiController]
[Route("api/webhooks/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly MuroDbContext _context;
    private readonly ILogger<TelegramWebhookController> _logger;
    private readonly ISupportService _supportService;

    public TelegramWebhookController(MuroDbContext context, ILogger<TelegramWebhookController> logger, ISupportService supportService)
    {
        _context = context;
        _logger = logger;
        _supportService = supportService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromBody] JsonElement payload)
    {
        try
        {
            if (payload.TryGetProperty("callback_query", out var callbackQuery))
            {
                await HandleCallbackQuery(callbackQuery);
            }
            else if (payload.TryGetProperty("message", out var message))
            {
                await HandleMessage(message);
            }

            return Ok(); // Telegram expects 200 OK
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram webhook");
            return Ok(); // Return 200 anyway so Telegram doesn't retry infinitely
        }
    }

    private async Task HandleCallbackQuery(JsonElement callbackQuery)
    {
        var data = callbackQuery.GetProperty("data").GetString();
        if (string.IsNullOrEmpty(data)) return;

        var parts = data.Split('_');
        if (parts.Length != 2) return;

        var action = parts[0];
        if (!Guid.TryParse(parts[1], out var ticketId)) return;

        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null) return;

        if (action == "close")
        {
            ticket.Status = TicketStatus.Closed;
        }
        else if (action == "high")
        {
            ticket.Priority = "high";
        }

        await _context.SaveChangesAsync();
    }

    private async Task HandleMessage(JsonElement message)
    {
        if (!message.TryGetProperty("reply_to_message", out var replyToMessage))
            return; // We only process replies

        var repliedMessageId = replyToMessage.GetProperty("message_id").GetInt64();
        
        var mapping = await _context.TelegramMessageMappings
            .FirstOrDefaultAsync(m => m.TelegramMessageId == repliedMessageId);
            
        if (mapping == null)
            return; // Not a reply to our notification

        var text = message.TryGetProperty("text", out var textProp) ? textProp.GetString() : string.Empty;
        if (string.IsNullOrEmpty(text)) return;

        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == mapping.SupportTicketId);
        if (ticket == null) return;

        // Find the SuperAdmin to assign as sender
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin);
        if (admin == null) return; // Cannot reply if no admin exists to attribute it to

        await _supportService.ReplyAsync(ticket.Id, admin.Id, new ReplyTicketRequest(text));
    }
}
