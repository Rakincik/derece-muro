using Microsoft.EntityFrameworkCore;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CourseEnrollmentService : ICourseEnrollmentService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public CourseEnrollmentService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task AssignToGroupAsync(Guid courseId, Guid groupId, string mode)
    {
        var exists = await _context.CourseGroups
            .AnyAsync(cg => cg.CourseId == courseId && cg.GroupId == groupId);
        if (exists) throw new InvalidOperationException("Bu ders zaten bu gruba atanmış.");

        if (!Enum.TryParse<CourseMode>(mode, true, out var courseMode))
            throw new ArgumentException($"Geçersiz mod: {mode}");

        _context.CourseGroups.Add(new CourseGroup
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            GroupId = groupId,
            Mode = courseMode
        });
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"groups:");
        await _cache.RemoveByPrefixAsync($"courses:");
    }

    public async Task RemoveFromGroupAsync(Guid courseId, Guid groupId)
    {
        var cg = await _context.CourseGroups
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.GroupId == groupId)
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        _context.CourseGroups.Remove(cg);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"groups:");
        await _cache.RemoveByPrefixAsync($"courses:");
    }
    public async Task AssignToStudentAsync(Guid courseId, Guid userId)
    {
        var exists = await _context.CourseStudents
            .AnyAsync(cs => cs.CourseId == courseId && cs.UserId == userId);
        if (exists) throw new InvalidOperationException("Öğrenci zaten bu derse doğrudan atanmış.");

        _context.CourseStudents.Add(new CourseStudent
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
    }

    public async Task RemoveFromStudentAsync(Guid courseId, Guid userId)
    {
        var cs = await _context.CourseStudents
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.UserId == userId)
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        _context.CourseStudents.Remove(cs);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
    }

    public async Task<List<MURO.Application.DTOs.Courses.CourseStudentListDto>> GetEnrolledStudentsAsync(Guid courseId)
    {
        return await _context.CourseStudents
            .AsNoTracking()
            .Include(cs => cs.User)
            .Where(cs => cs.CourseId == courseId )
            .Select(cs => new MURO.Application.DTOs.Courses.CourseStudentListDto(
                cs.UserId,
                cs.User.FirstName,
                cs.User.LastName,
                cs.User.Email,
                cs.AssignedAt,
                cs.ExpiresAt
            ))
            .ToListAsync();
    }

    public async Task UpdateStudentExpirationAsync(Guid courseId, Guid userId, DateTime? expiresAt)
    {
        var cs = await _context.CourseStudents
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.UserId == userId )
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        cs.ExpiresAt = expiresAt;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
    }

    public async Task<List<MURO.Application.DTOs.Courses.CourseListDto>> GetDirectCoursesByUserAsync(Guid userId)
    {
        return await _context.CourseStudents
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.UserId == userId )
            .Select(cs => new MURO.Application.DTOs.Courses.CourseListDto(
                cs.Course.Id, cs.Course.Title, cs.Course.Description, cs.Course.ThumbnailUrl,
                cs.Course.CourseType.ToString(), cs.Course.IsPublished, 0, 0,
                cs.Course.Order, cs.Course.StartDate, cs.Course.CreatedAt, cs.Course.UpdatedAt,
                cs.Course.InstructorId, null, null
            ))
            .ToListAsync();
    }
}
