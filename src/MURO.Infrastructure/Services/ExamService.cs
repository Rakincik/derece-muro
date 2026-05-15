using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Exams;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class ExamService : IExamService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public ExamService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // ── LIST ──
    public async Task<PagedResult<ExamListDto>> GetExamsAsync(Guid tenantId, int page, int pageSize, string? search, string? examType, string? status)
    {
        var cacheKey = $"{tenantId}:exams:list:{page}:{pageSize}:{search}:{examType}:{status}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Exams
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(e => e.Title.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(examType))
                query = query.Where(e => e.ExamType == examType);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(e => e.Status == status);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new ExamListDto(
                    e.Id, e.Title, e.Description, e.ExamType,
                    e.QuestionCount, e.OptionCount, e.DurationMinutes,
                    e.Status, e.StartDate, e.EndDate,
                    e.ExamAssignments.Count,
                    e.Results.Count,
                    e.Results.Any() ? (double?)Math.Round(e.Results.Average(r => r.Score), 1) : null,
                    e.CreatedAt, e.WrongPenaltyWeight, e.ResultMode, e.ResultPublishDate, e.MaxScore, e.VirtualParticipantCount))
                .ToListAsync();

            return new PagedResult<ExamListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(3));
    }

    // ── GET BY ID ──
    public async Task<ExamDetailDto> GetExamByIdAsync(Guid tenantId, Guid examId)
    {
        var exam = await _context.Exams
            .AsNoTracking()
            .Where(e => e.Id == examId && e.TenantId == tenantId)
            .Include(e => e.ExamAssignments)
            .Include(e => e.Results).ThenInclude(r => r.User)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        // Parse answer key
        Dictionary<int, string>? answerKey = null;
        if (!string.IsNullOrEmpty(exam.AnswerKeyJson))
            answerKey = JsonSerializer.Deserialize<Dictionary<int, string>>(exam.AnswerKeyJson);

        // Parse question weights
        Dictionary<int, double>? questionWeights = null;
        if (!string.IsNullOrEmpty(exam.QuestionWeightsJson))
            questionWeights = JsonSerializer.Deserialize<Dictionary<int, double>>(exam.QuestionWeightsJson);

        // Build assignments
        var assignments = new List<ExamAssignmentDto>();
        foreach (var ea in exam.ExamAssignments)
        {
            var targetName = ea.TargetType switch
            {
                "Group" => (await _context.Groups.FindAsync(ea.TargetId))?.Name ?? "—",
                "User" => await _context.Users.Where(u => u.Id == ea.TargetId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "—",
                _ => "—"
            };
            assignments.Add(new ExamAssignmentDto(ea.Id, ea.TargetType, ea.TargetId, targetName, ea.StartsAt, ea.EndsAt, ea.AssignedAt));
        }

        // Build result summary
        ExamResultSummaryDto? resultSummary = null;
        if (exam.Results.Any())
        {
            var results = exam.Results.Select(r => new ExamResultDto(
                r.Id, r.UserId,
                r.User.FirstName + " " + r.User.LastName,
                r.CorrectCount, r.WrongCount, r.EmptyCount,
                Math.Round(r.CorrectCount - (r.WrongCount * 0.25), 2),
                r.Score,
                r.SubmittedAt
            )).OrderByDescending(r => r.Score).ToList();

            var scores = exam.Results.Select(r => r.Score).ToList();
            var nets = results.Select(r => r.Net).ToList();

            resultSummary = new ExamResultSummaryDto(
                results.Count,
                Math.Round(scores.Average(), 1),
                Math.Round(nets.Average(), 2),
                scores.Max(),
                scores.Min(),
                results
            );
        }

        return new ExamDetailDto(
            exam.Id, exam.Title, exam.Description, exam.ExamType,
            exam.QuestionCount, exam.OptionCount, exam.DurationMinutes,
            exam.Status, exam.ShowResults, exam.PdfUrl, exam.SolutionPdfUrl,
            answerKey, exam.StartDate, exam.EndDate, exam.CreatedAt,
            assignments, resultSummary, exam.WrongPenaltyWeight,
            exam.ResultMode, exam.ResultPublishDate, questionWeights, exam.SectionsJson, 
            exam.MaxScore, exam.VirtualParticipantCount);
    }

    // ── CREATE ──
    public async Task<ExamListDto> CreateExamAsync(Guid tenantId, CreateExamRequest request)
    {
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title,
            Description = request.Description,
            ExamType = request.ExamType,
            QuestionCount = request.QuestionCount,
            OptionCount = request.OptionCount,
            DurationMinutes = request.DurationMinutes,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ShowResults = request.ShowResults,
            WrongPenaltyWeight = request.WrongPenaltyWeight,
            ResultMode = request.ResultMode,
            ResultPublishDate = request.ResultPublishDate,
            QuestionWeightsJson = request.QuestionWeights != null ? JsonSerializer.Serialize(request.QuestionWeights) : null,
            SectionsJson = request.SectionsJson,
            Status = "Taslak",
            MaxScore = request.MaxScore,
            VirtualParticipantCount = request.VirtualParticipantCount
        };

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        return new ExamListDto(exam.Id, exam.Title, exam.Description, exam.ExamType,
            exam.QuestionCount, exam.OptionCount, exam.DurationMinutes,
            exam.Status, exam.StartDate, exam.EndDate, 0, 0, null, exam.CreatedAt,
            exam.WrongPenaltyWeight, exam.ResultMode, exam.ResultPublishDate, exam.MaxScore, exam.VirtualParticipantCount);
    }

    // ── UPDATE ──
    public async Task<ExamListDto> UpdateExamAsync(Guid tenantId, Guid examId, UpdateExamRequest request)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamAssignments)
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == examId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        if (request.Title != null) exam.Title = request.Title;
        if (request.Description != null) exam.Description = request.Description;
        if (request.ExamType != null) exam.ExamType = request.ExamType;
        if (request.QuestionCount.HasValue) exam.QuestionCount = request.QuestionCount.Value;
        if (request.OptionCount.HasValue) exam.OptionCount = request.OptionCount.Value;
        if (request.DurationMinutes.HasValue) exam.DurationMinutes = request.DurationMinutes;
        if (request.StartDate.HasValue) exam.StartDate = request.StartDate;
        if (request.EndDate.HasValue) exam.EndDate = request.EndDate;
        if (request.ShowResults.HasValue) exam.ShowResults = request.ShowResults.Value;
        if (request.WrongPenaltyWeight.HasValue) exam.WrongPenaltyWeight = request.WrongPenaltyWeight.Value;
        if (request.ResultMode != null) exam.ResultMode = request.ResultMode;
        if (request.ResultPublishDate.HasValue) exam.ResultPublishDate = request.ResultPublishDate;
        if (request.QuestionWeights != null) exam.QuestionWeightsJson = JsonSerializer.Serialize(request.QuestionWeights);
        if (request.SectionsJson != null) exam.SectionsJson = request.SectionsJson;
        if (request.MaxScore.HasValue) exam.MaxScore = request.MaxScore.Value;
        if (request.VirtualParticipantCount.HasValue) exam.VirtualParticipantCount = request.VirtualParticipantCount.Value;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        var avgScore = exam.Results.Any() ? (double?)Math.Round(exam.Results.Average(r => r.Score), 1) : null;

        return new ExamListDto(exam.Id, exam.Title, exam.Description, exam.ExamType,
            exam.QuestionCount, exam.OptionCount, exam.DurationMinutes,
            exam.Status, exam.StartDate, exam.EndDate,
            exam.ExamAssignments.Count, exam.Results.Count, avgScore, exam.CreatedAt,
            exam.WrongPenaltyWeight, exam.ResultMode, exam.ResultPublishDate, exam.MaxScore, exam.VirtualParticipantCount);
    }

    // ── DELETE ──
    public async Task DeleteExamAsync(Guid tenantId, Guid examId)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == examId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");
    }

    // ── ANSWER KEY ──
    public async Task<ExamDetailDto> UpdateAnswerKeyAsync(Guid tenantId, Guid examId, UpdateAnswerKeyRequest request)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == examId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        exam.AnswerKeyJson = JsonSerializer.Serialize(request.AnswerKey);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        return await GetExamByIdAsync(tenantId, examId);
    }

    // ── STATUS ──
    public async Task<ExamListDto> UpdateStatusAsync(Guid tenantId, Guid examId, UpdateExamStatusRequest request)
    {
        var validStatuses = new[] { "Taslak", "Yayında", "Tamamlandı" };
        if (!validStatuses.Contains(request.Status))
            throw new ArgumentException($"Geçersiz durum: {request.Status}");

        var exam = await _context.Exams
            .Include(e => e.ExamAssignments)
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == examId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        exam.Status = request.Status;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        var avgScore = exam.Results.Any() ? (double?)Math.Round(exam.Results.Average(r => r.Score), 1) : null;

        return new ExamListDto(exam.Id, exam.Title, exam.Description, exam.ExamType,
            exam.QuestionCount, exam.OptionCount, exam.DurationMinutes,
            exam.Status, exam.StartDate, exam.EndDate,
            exam.ExamAssignments.Count, exam.Results.Count, avgScore, exam.CreatedAt,
            exam.WrongPenaltyWeight, exam.ResultMode, exam.ResultPublishDate, exam.MaxScore, exam.VirtualParticipantCount);
    }

    // ── PDF ──
    public async Task<ExamDetailDto> UpdatePdfAsync(Guid tenantId, Guid examId, UpdateExamPdfRequest request)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == examId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        if (request.PdfUrl != null) exam.PdfUrl = request.PdfUrl;
        if (request.SolutionPdfUrl != null) exam.SolutionPdfUrl = request.SolutionPdfUrl;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        return await GetExamByIdAsync(tenantId, examId);
    }


}
