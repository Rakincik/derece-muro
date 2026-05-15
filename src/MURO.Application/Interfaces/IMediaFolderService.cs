using MURO.Application.DTOs;
using MURO.Application.DTOs.Media;

namespace MURO.Application.Interfaces;

public interface IMediaFolderService
{
    Task<List<MediaFolderDto>> GetFoldersAsync(Guid tenantId, Guid? parentFolderId = null);
    Task<MediaFolderDto> GetFolderByIdAsync(Guid tenantId, Guid folderId);
    Task<MediaFolderDto> CreateFolderAsync(Guid tenantId, CreateMediaFolderRequest request);
    Task<MediaFolderDto> UpdateFolderAsync(Guid tenantId, Guid folderId, UpdateMediaFolderRequest request);
    Task DeleteFolderAsync(Guid tenantId, Guid folderId);
}
