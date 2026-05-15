using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Courses;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CourseMaterialService : ICourseMaterialService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public CourseMaterialService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CourseMaterialDto>> GetMaterialsAsync(Guid tenantId, Guid courseId)
    {
        return await _context.CourseMaterials
            .Where(m => m.CourseId == courseId && m.TenantId == tenantId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new CourseMaterialDto(m.Id, m.Title, m.FileName, m.FilePath, m.ContentType, m.FileSize, m.CreatedAt))
            .ToListAsync();
    }

    public async Task<CourseMaterialDto> UploadMaterialAsync(Guid tenantId, Guid courseId, Stream fileStream, string fileName, string contentType, long fileSize, string? title, string webRootPath)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Kurs bulunamadı.");

        var uploadsDir = Path.Combine(webRootPath, "uploads", "materials", tenantId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(stream);
        }

        var material = new Domain.Entities.CourseMaterial
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            TenantId = tenantId,
            Title = title ?? Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            FilePath = $"/uploads/materials/{tenantId}/{uniqueName}",
            ContentType = contentType,
            FileSize = fileSize,
            CreatedAt = DateTime.UtcNow
        };

        _context.CourseMaterials.Add(material);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:courses:");

        return new CourseMaterialDto(material.Id, material.Title, material.FileName, material.FilePath, material.ContentType, material.FileSize, material.CreatedAt);
    }

    public async Task DeleteMaterialAsync(Guid tenantId, Guid courseId, Guid materialId, string webRootPath)
    {
        var material = await _context.CourseMaterials
            .FirstOrDefaultAsync(m => m.Id == materialId && m.CourseId == courseId && m.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Doküman bulunamadı.");

        var diskPath = Path.Combine(webRootPath, material.FilePath.TrimStart('/'));
        if (File.Exists(diskPath)) File.Delete(diskPath);

        _context.CourseMaterials.Remove(material);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:courses:");
    }
}
