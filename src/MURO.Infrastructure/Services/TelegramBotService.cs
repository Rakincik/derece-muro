using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class TelegramBotService : ITelegramBotService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly MuroDbContext _dbContext;

    private readonly string _botToken;
    private readonly string _adminChatId;
    private readonly string _telegramApiUrl;

    public TelegramBotService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TelegramBotService> logger,
        MuroDbContext dbContext)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;

        _botToken = _configuration["Telegram:BotToken"] ?? string.Empty;
        _adminChatId = _configuration["Telegram:AdminChatId"] ?? string.Empty;
        _telegramApiUrl = $"https://api.telegram.org/bot{_botToken}";
    }

    public async Task SendNewTicketNotificationAsync(SupportTicket ticket)
    {
        if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminChatId))
        {
            _logger.LogWarning("Telegram configuration is missing. Cannot send notification.");
            return;
        }

        var userName = ticket.User != null ? $"{ticket.User.FirstName} {ticket.User.LastName}" : "Bilinmeyen Öğrenci";
        
        var text = $"🆕 *YENİ DESTEK TALEBİ*\n\n" +
                   $"👤 *Öğrenci:* {userName}\n" +
                   $"🎫 *Konu:* {ticket.Subject}\n" +
                   $"🚨 *Öncelik:* {ticket.Priority}\n" +
                   $"🏷 *Kategori:* {ticket.Category}\n\n" +
                   $"💬 *Mesaj:*\n_{ticket.Body}_";

        var payload = new
        {
            chat_id = _adminChatId,
            text = text,
            parse_mode = "Markdown",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚨 Acil İşaretle", callback_data = $"high_{ticket.Id}" },
                        new { text = "❌ Kapat", callback_data = $"close_{ticket.Id}" }
                    }
                }
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_telegramApiUrl}/sendMessage", content);

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(resultJson);
                var messageId = result.GetProperty("result").GetProperty("message_id").GetInt64();

                // Save mapping
                _dbContext.TelegramMessageMappings.Add(new TelegramMessageMapping
                {
                    TelegramMessageId = messageId,
                    TelegramChatId = long.Parse(_adminChatId),
                    SupportTicketId = ticket.Id
                });
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Telegram notification sent for ticket {ticket.Id}. Message ID: {messageId}");
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Telegram API error: {err}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification");
        }
    }

    public async Task SendReplyToTicketAsync(Guid ticketId, string replyText)
    {
        if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminChatId))
        {
            _logger.LogWarning("Telegram configuration is missing. Cannot send notification.");
            return;
        }

        try
        {
            var ticket = await _dbContext.SupportTickets
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
            {
                _logger.LogWarning($"Ticket {ticketId} not found when trying to send Telegram reply notification.");
                return;
            }

            var mapping = await _dbContext.TelegramMessageMappings
                .FirstOrDefaultAsync(m => m.SupportTicketId == ticketId);

            var userName = ticket.User != null ? $"{ticket.User.FirstName} {ticket.User.LastName}" : "Bilinmeyen Öğrenci";
            
            var text = $"💬 *YENİ MESAJ* (Konu: {ticket.Subject})\n\n" +
                       $"👤 *Gönderen:* {userName}\n\n" +
                       $"💬 *Cevap:*\n_{replyText}_";

            var payload = new
            {
                chat_id = _adminChatId,
                text = text,
                parse_mode = "Markdown",
                reply_to_message_id = mapping?.TelegramMessageId
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_telegramApiUrl}/sendMessage", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Telegram reply notification sent for ticket {ticketId}.");
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Telegram API error on reply: {err}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram reply notification");
        }
    }
}
