using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Exams;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class ExamResultService : IExamResultService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public ExamResultService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<ExamResultSummaryDto> GetExamResultsAsync(Guid tenantId, Guid examId)
    {
        var exam = await _context.Exams
            .AsNoTracking()
            .Where(e => e.Id == examId && e.TenantId == tenantId)
            .Include(e => e.Results).ThenInclude(r => r.User)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        var results = exam.Results.Select(r => new ExamResultDto(
            r.Id, r.UserId,
            r.User.FirstName + " " + r.User.LastName,
            r.CorrectCount, r.WrongCount, r.EmptyCount,
            Math.Round(r.CorrectCount - (r.WrongCount * exam.WrongPenaltyWeight), 2),
            r.Score,
            r.SubmittedAt,
            r.StartedAt,
            r.StartedAt.HasValue ? (int)(r.SubmittedAt - r.StartedAt.Value).TotalSeconds : null,
            CalculateSectionResults(r.Answers, exam.AnswerKeyJson, exam.SectionsJson, exam.WrongPenaltyWeight, exam.QuestionWeightsJson)
        )).OrderByDescending(r => r.Score).ToList();

        if (!results.Any())
        {
            return new ExamResultSummaryDto(0, 0, 0, 0, 0, results, new List<ScoreRangeDto>());
        }

        var scores = results.Select(r => r.Score).ToList();
        var nets = results.Select(r => r.Net).ToList();

        // Score distribution histogram
        var ranges = new[] { "0-20", "20-40", "40-60", "60-80", "80-100" };
        var scoreDistribution = ranges.Select((range, i) =>
        {
            var lo = i * 20;
            var hi = (i + 1) * 20;
            var count = scores.Count(s => s >= lo && (i == 4 ? s <= hi : s < hi));
            return new ScoreRangeDto(range, count);
        }).ToList();

        return new ExamResultSummaryDto(
            results.Count,
            Math.Round(scores.Average(), 1),
            Math.Round(nets.Average(), 2),
            scores.Max(),
            scores.Min(),
            results,
            scoreDistribution
        );
    }

    public async Task<ExamOverallSummaryDto> GetOverallSummaryAsync(Guid tenantId)
    {
        var cacheKey = $"{tenantId}:exams:overallsummary";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var examsWithResults = await _context.Exams.AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Results.Any())
                .Select(e => new
                {
                    e.ExamType,
                    ResultCount = e.Results.Count,
                    AvgScore = e.Results.Average(r => r.Score)
                })
                .ToListAsync();

            var totalParticipants = examsWithResults.Sum(e => e.ResultCount);
            var overallAvg = examsWithResults.Any()
                ? Math.Round(examsWithResults.Average(e => e.AvgScore), 1) : 0;
            var examTypes = examsWithResults.Select(e => e.ExamType).Distinct().Count();

            return new ExamOverallSummaryDto(
                examsWithResults.Count, totalParticipants, overallAvg, examTypes);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<ExamResultDto> SubmitAnswersAsync(Guid tenantId, Guid examId, Guid userId, SubmitExamAnswersRequest request)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == examId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Sınav bulunamadı.");

        if (string.IsNullOrEmpty(exam.AnswerKeyJson))
            throw new InvalidOperationException("Cevap anahtarı henüz girilmemiş.");

        if (await _context.ExamResults.AnyAsync(r => r.ExamId == examId && r.UserId == userId))
            throw new InvalidOperationException("Bu sınava daha önce katıldınız.");

        var answerKey = JsonSerializer.Deserialize<Dictionary<int, string>>(exam.AnswerKeyJson)!;

        // Parse question weights (katsayı) — default 1.0 per question
        Dictionary<int, double>? weights = null;
        if (!string.IsNullOrEmpty(exam.QuestionWeightsJson))
        {
            try { weights = JsonSerializer.Deserialize<Dictionary<int, double>>(exam.QuestionWeightsJson); }
            catch { /* ignore parse errors, use default 1.0 */ }
        }

        int correct = 0, wrong = 0, empty = 0;
        double weightedCorrect = 0, weightedWrong = 0, totalWeight = 0;

        for (int i = 1; i <= exam.QuestionCount; i++)
        {
            var w = weights != null && weights.TryGetValue(i, out var qw) ? qw : 1.0;
            totalWeight += w;

            if (!request.Answers.TryGetValue(i, out var studentAnswer) || string.IsNullOrEmpty(studentAnswer))
            {
                empty++;
            }
            else if (answerKey.TryGetValue(i, out var correctAnswer) && studentAnswer == correctAnswer)
            {
                correct++;
                weightedCorrect += w;
            }
            else
            {
                wrong++;
                weightedWrong += w;
            }
        }

        // Net hesaplama: Ağırlıklı Doğru - (Ağırlıklı Yanlış × WrongPenaltyWeight)
        var net = Math.Round(weightedCorrect - (weightedWrong * exam.WrongPenaltyWeight), 2);
        // Puan hesaplama: (Net / Toplam Ağırlık) × 100
        var rawScore = totalWeight > 0 ? (net / totalWeight) * 100 : 0;
        var score = Math.Round(rawScore + exam.MaxScore, 1);
        if (score < exam.MaxScore && net <= 0) score = exam.MaxScore; // "Net negatife düştüğünde verilecek minimum puan"

        var result = new ExamResult
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            UserId = userId,
            Answers = JsonSerializer.Serialize(request.Answers),
            CorrectCount = correct,
            WrongCount = wrong,
            EmptyCount = empty,
            Score = score,
            StartedAt = request.StartedAt
        };

        _context.ExamResults.Add(result);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        var user = await _context.Users.FindAsync(userId);
        var fullName = user != null ? $"{user.FirstName} {user.LastName}" : "—";
        int? durationSeconds = null;
        if (result.StartedAt.HasValue)
        {
            durationSeconds = (int)(result.SubmittedAt - result.StartedAt.Value).TotalSeconds;
        }

        var sectionResults = CalculateSectionResults(result.Answers, exam.AnswerKeyJson, exam.SectionsJson, exam.WrongPenaltyWeight, exam.QuestionWeightsJson);

        return new ExamResultDto(result.Id, result.UserId, fullName,
            correct, wrong, empty, net, score, result.SubmittedAt, result.StartedAt, durationSeconds, sectionResults);
    }

    public async Task<List<MyExamResultDto>> GetMyExamResultsAsync(Guid tenantId, Guid userId)
    {
        var myResults = await _context.ExamResults.AsNoTracking()
            .Where(r => r.UserId == userId && r.Exam.TenantId == tenantId)
            .Include(r => r.Exam)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();

        var list = new List<MyExamResultDto>();
        foreach (var r in myResults)
        {
            // Sınıf ortalaması ve sıralama (showResults flag'i aktifse)
            double? avgScore = null;
            int? rank = null;
            if (r.Exam.ShowResults)
            {
                var allScores = await _context.ExamResults.AsNoTracking()
                    .Where(x => x.ExamId == r.ExamId)
                    .Select(x => x.Score)
                    .ToListAsync();
                if (allScores.Any())
                {
                    avgScore = Math.Round(allScores.Average(), 1);
                    var sorted = allScores.OrderByDescending(s => s).ToList();
                    rank = sorted.IndexOf(r.Score) + 1;
                }
            }

            // Net: entity'de saklanmıyor, anlık hesaplanıyor
            var net = Math.Round(r.CorrectCount - (r.WrongCount * r.Exam.WrongPenaltyWeight), 2);

            var sectionResults = CalculateSectionResults(r.Answers, r.Exam.AnswerKeyJson, r.Exam.SectionsJson, r.Exam.WrongPenaltyWeight, r.Exam.QuestionWeightsJson);

            list.Add(new MyExamResultDto(
                r.ExamId, r.Exam.Title, r.Exam.ExamType.ToString(),
                r.Exam.QuestionCount, r.CorrectCount, r.WrongCount, r.EmptyCount,
                net, r.Score, avgScore, rank, r.SubmittedAt, r.Exam.ShowResults, sectionResults));
        }
        return list;
    }

    private class ExamSectionModel
    {
        public string name { get; set; } = string.Empty;
        public int start { get; set; }
        public int end { get; set; }
    }

    private Dictionary<string, SectionResultDto>? CalculateSectionResults(string? answersJson, string? answerKeyJson, string? sectionsJson, double penalty, string? weightsJson)
    {
        if (string.IsNullOrEmpty(answersJson) || string.IsNullOrEmpty(answerKeyJson) || string.IsNullOrEmpty(sectionsJson))
            return null;

        try
        {
            var answers = JsonSerializer.Deserialize<Dictionary<int, string>>(answersJson);
            var key = JsonSerializer.Deserialize<Dictionary<int, string>>(answerKeyJson);
            var sections = JsonSerializer.Deserialize<List<ExamSectionModel>>(sectionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (answers == null || key == null || sections == null || !sections.Any()) return null;

            var result = new Dictionary<string, SectionResultDto>();

            foreach (var sec in sections)
            {
                int correct = 0, wrong = 0, empty = 0;
                for (int i = sec.start; i <= sec.end; i++)
                {
                    if (!answers.TryGetValue(i, out var studentAns) || string.IsNullOrEmpty(studentAns))
                    {
                        empty++;
                    }
                    else if (key.TryGetValue(i, out var correctAns) && studentAns == correctAns)
                    {
                        correct++;
                    }
                    else
                    {
                        wrong++;
                    }
                }

                double net = Math.Round(correct - (wrong * penalty), 2);
                result[sec.name] = new SectionResultDto(sec.name, correct, wrong, empty, net);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }
}
