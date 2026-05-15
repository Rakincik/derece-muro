using MURO.Application.DTOs;
using MURO.Application.DTOs.Podcasts;

namespace MURO.Application.Interfaces;

public interface IPodcastService
{
    Task<PagedResult<PodcastDto>> GetPodcastsAsync(Guid tenantId, int page, int pageSize, Guid? courseId);
    Task<PodcastDto> GetByIdAsync(Guid tenantId, Guid podcastId);
    Task<PodcastDto> CreateAsync(Guid tenantId, CreatePodcastRequest request);
    Task<PodcastDto> UpdateStatusAsync(Guid tenantId, Guid podcastId, string status);
    Task DeleteAsync(Guid tenantId, Guid podcastId);

    /// <summary>
    /// Ham metinden AI podcast üretir: Gemini → TTS → MP3 kayıt.
    /// </summary>
    Task<PodcastDto> GenerateAsync(Guid tenantId, GeneratePodcastRequest request);

    /// <summary>
    /// Anonim ses streaming için tenant sız podcast getirir.
    /// </summary>
    Task<PodcastDto?> GetByIdForStreamAsync(Guid podcastId);
}
