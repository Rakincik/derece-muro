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

    public async Task AssignToGroupAsync(Guid tenantId, Guid courseId, Guid groupId, string mode)
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
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:courses:");
    }

    public async Task RemoveFromGroupAsync(Guid tenantId, Guid courseId, Guid groupId)
    {
        var cg = await _context.CourseGroups
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.GroupId == groupId)
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        _context.CourseGroups.Remove(cg);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:courses:");
    }
}
