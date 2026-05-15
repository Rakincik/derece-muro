using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Podcasts;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class PodcastService : IPodcastService
{
    private readonly MuroDbContext _context;
    private readonly IGeminiService _gemini;
    private readonly EdgeTtsClient _edgeTts;
    private readonly string _wwwrootPath;
    private readonly ICacheService _cache;

    public PodcastService(
        MuroDbContext context,
        IGeminiService gemini,
        EdgeTtsClient edgeTts,
        IConfiguration config,
        ICacheService cache)
    {
        _context     = context;
        _gemini      = gemini;
        _edgeTts     = edgeTts;
        _cache       = cache;
        // wwwrootPath: config'den oku, yoksa cwd/wwwroot
        _wwwrootPath = config["Podcast:WwwrootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }


    // ─── AI Generate ──────────────────────────────────────────────────────────

    public async Task<PodcastDto> GenerateAsync(Guid tenantId, GeneratePodcastRequest request)
    {
        // 1. DB kaydı oluştur (Processing)
        var podcast = new Podcast
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            CourseId    = request.CourseId,
            Title       = request.Title,
            TextContent = request.RawText,
            Status      = MediaStatus.Processing,
            CreatedAt   = DateTime.UtcNow
        };
        _context.Podcasts.Add(podcast);
        await _context.SaveChangesAsync();

        try
        {
            // 2. Gemini ile script oluştur
            var script = await _gemini.EnhanceToScriptAsync(request.RawText);
            podcast.GeneratedScript = script;

            // 3. edge-tts ile sese çevir
            var (path, duration) = await _edgeTts.SynthesizeAsync(script, request.Voice, _wwwrootPath);
            podcast.AudioFilePath   = path;
            podcast.DurationSeconds = duration;
            podcast.Status          = MediaStatus.Ready;
        }
        catch
        {
            podcast.Status = MediaStatus.Failed;
            throw;
        }
        finally
        {
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"{tenantId}:podcasts:");
        }

        string? courseTitle = null;
        if (podcast.CourseId.HasValue)
            courseTitle = await _context.Courses
                .Where(c => c.Id == podcast.CourseId)
                .Select(c => c.Title)
                .FirstOrDefaultAsync();

        return MapToDto(podcast, courseTitle);
    }

    // ─── Stream (anonymous) ───────────────────────────────────────────────────

    public async Task<PodcastDto?> GetByIdForStreamAsync(Guid podcastId)
    {
        var podcast = await _context.Podcasts
            .AsNoTracking()
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == podcastId);

        return podcast is null ? null : MapToDto(podcast, podcast.Course?.Title);
    }

    // ─── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<PodcastDto>> GetPodcastsAsync(Guid tenantId, int page, int pageSize, Guid? courseId)
    {
        var cacheKey = $"{tenantId}:podcasts:list:{page}:{pageSize}:{courseId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Podcasts
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId);

            if (courseId.HasValue)
                query = query.Where(p => p.CourseId == courseId);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.Course)
                .ToListAsync();

            return new PagedResult<PodcastDto>(
                items.Select(p => MapToDto(p, p.Course?.Title)).ToList(),
                totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<PodcastDto> GetByIdAsync(Guid tenantId, Guid podcastId)
    {
        var podcast = await _context.Podcasts
            .AsNoTracking()
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == podcastId && p.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Podcast bulunamadı.");

        return MapToDto(podcast, podcast.Course?.Title);
    }

    public async Task<PodcastDto> CreateAsync(Guid tenantId, CreatePodcastRequest request)
    {
        var podcast = new Podcast
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            CourseId      = request.CourseId,
            Title         = request.Title,
            TextContent   = request.TextContent,
            AudioFilePath = request.AudioFilePath,
            Status        = MediaStatus.Processing,
            CreatedAt     = DateTime.UtcNow
        };

        _context.Podcasts.Add(podcast);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:podcasts:");

        string? courseTitle = null;
        if (request.CourseId.HasValue)
            courseTitle = await _context.Courses
                .Where(c => c.Id == request.CourseId)
                .Select(c => c.Title)
                .FirstOrDefaultAsync();

        return MapToDto(podcast, courseTitle);
    }

    public async Task<PodcastDto> UpdateStatusAsync(Guid tenantId, Guid podcastId, string status)
    {
        var podcast = await _context.Podcasts
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == podcastId && p.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Podcast bulunamadı.");

        if (Enum.TryParse<MediaStatus>(status, true, out var mediaStatus))
            podcast.Status = mediaStatus;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:podcasts:");
        return MapToDto(podcast, podcast.Course?.Title);
    }

    public async Task DeleteAsync(Guid tenantId, Guid podcastId)
    {
        var podcast = await _context.Podcasts
            .FirstOrDefaultAsync(p => p.Id == podcastId && p.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Podcast bulunamadı.");

        // Ses dosyasını da sil
        if (!string.IsNullOrEmpty(podcast.AudioFilePath))
        {
            var fullPath = Path.Combine(_wwwrootPath, podcast.AudioFilePath.TrimStart('/'));
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }

        _context.Podcasts.Remove(podcast);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:podcasts:");
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static PodcastDto MapToDto(Podcast p, string? courseTitle) => new(
        p.Id, p.CourseId, courseTitle,
        p.Title, p.TextContent, p.GeneratedScript, p.AudioFilePath,
        p.DurationSeconds, p.Status, p.CreatedAt);
}
