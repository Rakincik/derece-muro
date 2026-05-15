using MURO.Application.DTOs;
using MURO.Application.DTOs.Media;

namespace MURO.Application.Interfaces;

public interface IMediaService
{
    // Media Assets
    Task<PagedResult<MediaAssetDto>> GetAssetsAsync(
        Guid tenantId, int page, int pageSize, Guid? courseId, string? search = null, Guid? userId = null, Guid? folderId = null, bool excludeRecordings = false);
    Task<MediaAssetDto> GetAssetByIdAsync(Guid tenantId, Guid assetId, Guid? userId = null);
    Task<MediaAssetDto> CreateAssetAsync(Guid tenantId, CreateMediaAssetRequest request);
    Task<MediaAssetDto> UpdateAssetAsync(Guid tenantId, Guid assetId, UpdateMediaAssetRequest request);
    Task DeleteAssetAsync(Guid tenantId, Guid assetId);
    Task<List<Guid>> GetAssignedCourseIdsAsync(Guid tenantId, Guid mediaAssetId);
    
    // Video Progress
    Task<VideoProgressDto> GetProgressAsync(Guid userId, Guid mediaAssetId);
    Task<VideoProgressDto> UpdateProgressAsync(Guid userId, Guid mediaAssetId, UpdateVideoProgressRequest request);
    
    // Podcasts
    Task<PagedResult<PodcastDto>> GetPodcastsAsync(Guid tenantId, int page, int pageSize);
    Task<PodcastDto> CreatePodcastAsync(Guid tenantId, CreatePodcastRequest request);
}
