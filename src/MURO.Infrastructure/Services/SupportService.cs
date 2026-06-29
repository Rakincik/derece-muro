using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Support;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class SupportService : ISupportService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public SupportService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<TicketListDto>> GetTicketsAsync(int page, int pageSize, string? status, Guid? userId = null)
    {
        var cacheKey = $"support:tickets:{page}:{pageSize}:{status}:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.SupportTickets.AsNoTracking().Where(t => true);
            if (userId.HasValue)
                query = query.Where(t => t.UserId == userId.Value);
            
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, true, out var ts))
                query = query.Where(t => t.Status == ts);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query.OrderByDescending(t => t.Messages.Any() ? t.Messages.Max(m => m.CreatedAt) : t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Include(t => t.User).Include(t => t.Messages)
                .Select(t => new TicketListDto(t.Id, t.Subject, t.Status.ToString(), t.Priority,
                    t.Category, t.User.FirstName + " " + t.User.LastName, t.Messages.Count, 
                    t.Messages.Any() ? t.Messages.Max(m => m.CreatedAt) : t.CreatedAt))
                .ToListAsync();

            return new PagedResult<TicketListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<TicketDetailDto> GetTicketByIdAsync(Guid ticketId, Guid? userId = null)
    {
        var cacheKey = $"support:ticket:{ticketId}:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.SupportTickets.AsNoTracking().Where(t => t.Id == ticketId);
            if (userId.HasValue)
                query = query.Where(t => t.UserId == userId.Value);

            var t = await query
                .Include(t => t.User)
                .Include(t => t.Messages.OrderBy(m => m.CreatedAt)).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

            return new TicketDetailDto(t.Id, t.Subject, t.Body, t.Status.ToString(), t.Priority, t.Category,
                t.UserId, $"{t.User.FirstName} {t.User.LastName}",
                t.Messages.Select(m => new TicketMessageDto(
                    m.Id, 
                    m.SenderId,
                    $"{m.Sender.FirstName} {m.Sender.LastName}", 
                    m.Body, 
                    m.CreatedAt,
                    m.Sender.Role == UserRole.Admin || m.Sender.Role == UserRole.SuperAdmin || m.Sender.Role == UserRole.Assistant
                )).ToList(),
                t.CreatedAt);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<TicketListDto> CreateTicketAsync(Guid userId, CreateTicketRequest request)
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(), UserId = userId,
            Subject = request.Subject, Body = request.Body,
            Priority = request.Priority, Category = request.Category
        };
        var msg = new SupportMessage
        {
            Id = Guid.NewGuid(), TicketId = ticket.Id,
            SenderId = userId, Body = request.Body
        };
        _context.SupportTickets.Add(ticket);
        _context.SupportMessages.Add(msg);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"support:");

        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        return new TicketListDto(ticket.Id, ticket.Subject, ticket.Status.ToString(),
            ticket.Priority, ticket.Category, $"{user.FirstName} {user.LastName}", 0, ticket.CreatedAt);
    }

    public async Task<TicketMessageDto> ReplyAsync(Guid ticketId, Guid senderId, ReplyTicketRequest request)
    {
        var ticket = await _context.SupportTickets.FindAsync(ticketId)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

        var msg = new SupportMessage
        {
            Id = Guid.NewGuid(), TicketId = ticketId,
            SenderId = senderId, Body = request.Body
        };
        _context.SupportMessages.Add(msg);
        ticket.Status = TicketStatus.InProgress;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"support:");

        var sender = await _context.Users.FindAsync(senderId);
        var isAdmin = sender?.Role == UserRole.Admin || sender?.Role == UserRole.SuperAdmin || sender?.Role == UserRole.Assistant;
        return new TicketMessageDto(msg.Id, senderId, $"{sender?.FirstName} {sender?.LastName}", msg.Body, msg.CreatedAt, isAdmin);
    }

    public async Task UpdateTicketStatusAsync(Guid ticketId, string status)
    {
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

        if (Enum.TryParse<TicketStatus>(status, true, out var parsedStatus))
        {
            ticket.Status = parsedStatus;
        }
        else
        {
            throw new ArgumentException("Geçersiz bilet durumu.");
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("support:");
    }

    public async Task DeleteTicketAsync(Guid ticketId)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");
        
        _context.SupportMessages.RemoveRange(ticket.Messages);
        _context.SupportTickets.Remove(ticket);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"support:");
    }

    // --- FAQ ---
    public async Task<List<FaqDto>> GetFaqsAsync()
    {
        var cacheKey = $"support:faqs";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.Faqs.AsNoTracking().Where(f => true)
                .OrderBy(f => f.SortOrder)
                .Select(f => new FaqDto(f.Id, f.QuestionText, f.AnswerText, f.Category, f.SortOrder))
                .ToListAsync();
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<FaqDto> CreateFaqAsync(CreateFaqRequest request)
    {
        var maxOrder = await _context.Faqs.Where(f => true).MaxAsync(f => (int?)f.SortOrder) ?? 0;
        var faq = new Faq
        {
            Id = Guid.NewGuid(), QuestionText = request.QuestionText, AnswerText = request.AnswerText,
            Category = request.Category, SortOrder = request.SortOrder ?? maxOrder + 1
        };
        _context.Faqs.Add(faq);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"support:");
        return new FaqDto(faq.Id, faq.QuestionText, faq.AnswerText, faq.Category, faq.SortOrder);
    }

    public async Task<FaqDto> UpdateFaqAsync(Guid faqId, UpdateFaqRequest request)
    {
        var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == faqId )
            ?? throw new KeyNotFoundException("SSS bulunamadı.");
        if (request.QuestionText != null) faq.QuestionText = request.QuestionText;
        if (request.AnswerText != null) faq.AnswerText = request.AnswerText;
        if (request.Category != null) faq.Category = request.Category;
        if (request.SortOrder.HasValue) faq.SortOrder = request.SortOrder.Value;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"support:");
        return new FaqDto(faq.Id, faq.QuestionText, faq.AnswerText, faq.Category, faq.SortOrder);
    }

    public async Task DeleteFaqAsync(Guid faqId)
    {
        var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == faqId )
            ?? throw new KeyNotFoundException("SSS bulunamadı.");
        _context.Faqs.Remove(faq);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"support:");
    }
}
