using MURO.Application.DTOs;
using MURO.Application.DTOs.Media;

namespace MURO.Application.Interfaces;

public interface ICourseMediaService
{
    Task<List<CourseMediaDto>> GetCourseMediasAsync(Guid tenantId, Guid courseId);
    Task<CourseMediaDto> AssignMediaAsync(Guid tenantId, Guid courseId, AssignMediaToCourseRequest request);
    Task BulkAssignFolderAsync(Guid tenantId, Guid courseId, BulkAssignFolderToCourseRequest request);
    Task RemoveMediaAsync(Guid tenantId, Guid courseId, Guid mediaAssetId);
    Task ReorderMediasAsync(Guid tenantId, Guid courseId, ReorderCourseMediaRequest request);
}
