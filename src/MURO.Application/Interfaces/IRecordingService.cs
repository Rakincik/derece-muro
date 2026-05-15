using MURO.Application.DTOs.Videos;

namespace MURO.Application.Interfaces;

public interface IRecordingService
{
    Task<List<SessionRecordingDto>> GetRecordingsAsync(Guid tenantId, Guid userId, string? role);
    Task DeleteRecordingAsync(Guid tenantId, Guid id);
}
