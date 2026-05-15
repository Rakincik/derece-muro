using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Exams;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class ExamAssignmentService : IExamAssignmentService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public ExamAssignmentService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<ExamAssignmentDto> AssignExamAsync(Guid tenantId, Guid examId, CreateExamAssignmentRequest request)
    {
        var exists = await _context.Exams.AnyAsync(e => e.Id == examId && e.TenantId == tenantId);
        if (!exists) throw new KeyNotFoundException("Sınav bulunamadı.");

        var assignment = new ExamAssignment
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt
        };

        _context.ExamAssignments.Add(assignment);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");

        // Resolve target name
        var targetName = request.TargetType switch
        {
            "Group" => (await _context.Groups.FindAsync(request.TargetId))?.Name ?? "—",
            "User" => await _context.Users.Where(u => u.Id == request.TargetId)
                .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "—",
            _ => "—"
        };

        return new ExamAssignmentDto(assignment.Id, assignment.TargetType, assignment.TargetId,
            targetName, assignment.StartsAt, assignment.EndsAt, assignment.AssignedAt);
    }

    public async Task RemoveAssignmentAsync(Guid tenantId, Guid examId, Guid assignmentId)
    {
        var assignment = await _context.ExamAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ExamId == examId)
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        _context.ExamAssignments.Remove(assignment);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:exams:");
    }
}
