using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;

namespace MURO.Application.Interfaces;

public interface ICourseSessionService
{
    Task<SessionDto> CreateSessionAsync(Guid tenantId, Guid courseId, CreateSessionRequest request);
    Task<SessionDto> UpdateSessionAsync(Guid tenantId, Guid courseId, Guid sessionId, UpdateSessionRequest request);
    Task DeleteSessionAsync(Guid tenantId, Guid courseId, Guid sessionId);
    Task ReorderSessionsAsync(Guid tenantId, Guid courseId, List<Guid> sessionIds);
    Task<List<UpcomingSessionDto>> GetUpcomingSessionsAsync(Guid tenantId);
    Task<List<UpcomingSessionDto>> GetUpcomingSessionsByUserAsync(Guid tenantId, Guid userId);
    Task<SessionDto> CreateVodSessionAsync(Guid tenantId, Guid courseId, string title, string filePath, int? durationSeconds = null);

}
