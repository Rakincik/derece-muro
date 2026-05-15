using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;

namespace MURO.Application.Interfaces;

public interface ICourseService
{
    // Courses
    Task<PagedResult<CourseListDto>> GetCoursesAsync(Guid tenantId, int page, int pageSize, string? search, string? courseType, bool? isPublished, Guid? instructorId = null);
    /// <summary>Student spesifik: sadece kullanıcının grubundaki dersler</summary>
    Task<PagedResult<CourseListDto>> GetCoursesByUserAsync(Guid tenantId, Guid userId, int page, int pageSize, string? search, string? courseType);
    Task<CourseDetailDto> GetCourseByIdAsync(Guid tenantId, Guid courseId, Guid? userId = null);
    Task<CourseListDto> CreateCourseAsync(Guid tenantId, CreateCourseRequest request);
    Task<CourseListDto> UpdateCourseAsync(Guid tenantId, Guid courseId, UpdateCourseRequest request);
    Task DeleteCourseAsync(Guid tenantId, Guid courseId);
}
