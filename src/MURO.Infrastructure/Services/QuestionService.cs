using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Questions;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class QuestionService : IQuestionService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public QuestionService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    private static QuestionDto ToDto(Question q, string userFull, string instructorFull, string? courseTitle) =>
        new(q.Id, q.UserId, userFull,
            q.InstructorId, instructorFull,
            q.Subject, q.Body, q.ImageUrl, q.AudioUrl,
            q.CourseId, courseTitle,
            q.Answer, q.AnsweredAt, q.Status, q.CreatedAt, q.Note);

    public async Task<PagedResult<QuestionDto>> GetQuestionsAsync(Guid tenantId, int page, int pageSize, string? status, Guid? instructorId)
    {
        var cacheKey = $"{tenantId}:questions:list:{page}:{pageSize}:{status}:{instructorId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Questions.AsNoTracking().Where(q => q.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(q => q.Status == status);
            if (instructorId.HasValue) query = query.Where(q => q.InstructorId == instructorId);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query.OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Include(q => q.User).Include(q => q.Instructor).Include(q => q.Course)
                .Select(q => new QuestionDto(
                    q.Id, q.UserId, q.User.FirstName + " " + q.User.LastName,
                    q.InstructorId, q.Instructor.FirstName + " " + q.Instructor.LastName,
                    q.Subject, q.Body, q.ImageUrl, q.AudioUrl, q.CourseId,
                    q.Course != null ? q.Course.Title : null,
                    q.Answer, q.AnsweredAt, q.Status, q.CreatedAt, q.Note))
                .ToListAsync();

            return new PagedResult<QuestionDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<QuestionDto> GetByIdAsync(Guid tenantId, Guid questionId)
    {
        return await _context.Questions.AsNoTracking()
            .Where(q => q.Id == questionId && q.TenantId == tenantId)
            .Include(q => q.User).Include(q => q.Instructor).Include(q => q.Course)
            .Select(q => new QuestionDto(
                q.Id, q.UserId, q.User.FirstName + " " + q.User.LastName,
                q.InstructorId, q.Instructor.FirstName + " " + q.Instructor.LastName,
                q.Subject, q.Body, q.ImageUrl, q.AudioUrl, q.CourseId,
                q.Course != null ? q.Course.Title : null,
                q.Answer, q.AnsweredAt, q.Status, q.CreatedAt, q.Note))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Soru bulunamadı.");
    }

    public async Task<QuestionDto> AskAsync(Guid tenantId, Guid userId, CreateQuestionRequest request)
    {
        var q = new Question
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
            InstructorId = request.InstructorId, Subject = request.Subject,
            Body = request.Body, ImageUrl = request.ImageUrl,
            AudioUrl = request.AudioUrl, Note = request.Note,
            CourseId = request.CourseId
        };
        _context.Questions.Add(q);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:questions:");

        var user = await _context.Users.FindAsync(userId);
        var instructor = await _context.Users.FindAsync(request.InstructorId);
        string? courseTitle = null;
        if (q.CourseId.HasValue)
            courseTitle = await _context.Courses.Where(c => c.Id == q.CourseId).Select(c => c.Title).FirstOrDefaultAsync();

        return ToDto(q, $"{user?.FirstName} {user?.LastName}", $"{instructor?.FirstName} {instructor?.LastName}", courseTitle);
    }

    public async Task<QuestionDto> AnswerAsync(Guid tenantId, Guid questionId, AnswerQuestionRequest request)
    {
        var q = await _context.Questions.Include(q => q.User).Include(q => q.Instructor)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Soru bulunamadı.");

        q.Answer = request.Answer;
        q.AnsweredAt = DateTime.UtcNow;
        q.Status = "answered";
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:questions:");

        return ToDto(q, $"{q.User.FirstName} {q.User.LastName}", $"{q.Instructor.FirstName} {q.Instructor.LastName}", q.Course?.Title);
    }

    public async Task<QuestionDto> UpdateNoteAsync(Guid tenantId, Guid questionId, Guid userId, UpdateNoteRequest request)
    {
        var q = await _context.Questions.Include(q => q.User).Include(q => q.Instructor)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TenantId == tenantId && q.UserId == userId)
            ?? throw new KeyNotFoundException("Soru bulunamadı.");

        q.Note = request.Note;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:questions:");

        return ToDto(q, $"{q.User.FirstName} {q.User.LastName}", $"{q.Instructor.FirstName} {q.Instructor.LastName}", q.Course?.Title);
    }
}
