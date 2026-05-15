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

    public async Task<PagedResult<TicketListDto>> GetTicketsAsync(Guid tenantId, int page, int pageSize, string? status)
    {
        var cacheKey = $"{tenantId}:support:tickets:{page}:{pageSize}:{status}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.SupportTickets.AsNoTracking().Where(t => t.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, true, out var ts))
                query = query.Where(t => t.Status == ts);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query.OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Include(t => t.User).Include(t => t.Messages)
                .Select(t => new TicketListDto(t.Id, t.Subject, t.Status.ToString(), t.Priority,
                    t.Category, t.User.FirstName + " " + t.User.LastName, t.Messages.Count, t.CreatedAt))
                .ToListAsync();

            return new PagedResult<TicketListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<TicketDetailDto> GetTicketByIdAsync(Guid tenantId, Guid ticketId)
    {
        var cacheKey = $"{tenantId}:support:ticket:{ticketId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var t = await _context.SupportTickets.AsNoTracking()
                .Where(t => t.Id == ticketId && t.TenantId == tenantId)
                .Include(t => t.User)
                .Include(t => t.Messages.OrderBy(m => m.CreatedAt)).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

            return new TicketDetailDto(t.Id, t.Subject, t.Body, t.Status.ToString(), t.Priority, t.Category,
                t.UserId, $"{t.User.FirstName} {t.User.LastName}",
                t.Messages.Select(m => new TicketMessageDto(m.Id, m.SenderId,
                    $"{m.Sender.FirstName} {m.Sender.LastName}", m.Body, m.CreatedAt)).ToList(),
                t.CreatedAt);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<TicketListDto> CreateTicketAsync(Guid tenantId, Guid userId, CreateTicketRequest request)
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
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
        await _cache.RemoveByPrefixAsync($"{tenantId}:support:");

        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        return new TicketListDto(ticket.Id, ticket.Subject, ticket.Status.ToString(),
            ticket.Priority, ticket.Category, $"{user.FirstName} {user.LastName}", 0, ticket.CreatedAt);
    }

    public async Task<TicketMessageDto> ReplyAsync(Guid tenantId, Guid ticketId, Guid senderId, ReplyTicketRequest request)
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
        await _cache.RemoveByPrefixAsync($"{tenantId}:support:");

        var sender = await _context.Users.FindAsync(senderId);
        return new TicketMessageDto(msg.Id, senderId, $"{sender?.FirstName} {sender?.LastName}", msg.Body, msg.CreatedAt);
    }

    public async Task CloseTicketAsync(Guid tenantId, Guid ticketId)
    {
        var t = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");
        t.Status = TicketStatus.Closed;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:support:");
    }

    // --- FAQ ---
    public async Task<List<FaqDto>> GetFaqsAsync(Guid tenantId)
    {
        var cacheKey = $"{tenantId}:support:faqs";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.Faqs.AsNoTracking().Where(f => f.TenantId == tenantId)
                .OrderBy(f => f.SortOrder)
                .Select(f => new FaqDto(f.Id, f.QuestionText, f.AnswerText, f.Category, f.SortOrder))
                .ToListAsync();
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<FaqDto> CreateFaqAsync(Guid tenantId, CreateFaqRequest request)
    {
        var maxOrder = await _context.Faqs.Where(f => f.TenantId == tenantId).MaxAsync(f => (int?)f.SortOrder) ?? 0;
        var faq = new Faq
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            QuestionText = request.QuestionText, AnswerText = request.AnswerText,
            Category = request.Category, SortOrder = request.SortOrder ?? maxOrder + 1
        };
        _context.Faqs.Add(faq);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:support:");
        return new FaqDto(faq.Id, faq.QuestionText, faq.AnswerText, faq.Category, faq.SortOrder);
    }

    public async Task<FaqDto> UpdateFaqAsync(Guid tenantId, Guid faqId, UpdateFaqRequest request)
    {
        var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == faqId && f.TenantId == tenantId)
            ?? throw new KeyNotFoundException("SSS bulunamadı.");
        if (request.QuestionText != null) faq.QuestionText = request.QuestionText;
        if (request.AnswerText != null) faq.AnswerText = request.AnswerText;
        if (request.Category != null) faq.Category = request.Category;
        if (request.SortOrder.HasValue) faq.SortOrder = request.SortOrder.Value;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:support:");
        return new FaqDto(faq.Id, faq.QuestionText, faq.AnswerText, faq.Category, faq.SortOrder);
    }

    public async Task DeleteFaqAsync(Guid tenantId, Guid faqId)
    {
        var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == faqId && f.TenantId == tenantId)
            ?? throw new KeyNotFoundException("SSS bulunamadı.");
        _context.Faqs.Remove(faq);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:support:");
    }
}
