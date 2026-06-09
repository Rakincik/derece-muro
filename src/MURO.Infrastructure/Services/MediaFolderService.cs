using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class MediaFolderService : IMediaFolderService
{
    private readonly MuroDbContext _context;
    private readonly IMediaService _mediaService;

    public MediaFolderService(MuroDbContext context, IMediaService mediaService)
    {
        _context = context;
        _mediaService = mediaService;
    }

    public async Task<List<MediaFolderDto>> GetFoldersAsync(Guid tenantId, Guid? parentFolderId = null)
    {
        var folders = await _context.MediaFolders
            .Where(f => f.TenantId == tenantId && f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.Name)
            .Select(f => new MediaFolderDto(
                f.Id,
                f.Name,
                f.ParentFolderId,
                f.CreatedAt,
                f.SubFolders.Count,
                f.MediaAssets.Count
            ))
            .ToListAsync();

        return folders;
    }

    public async Task<MediaFolderDto> GetFolderByIdAsync(Guid tenantId, Guid folderId)
    {
        var folder = await _context.MediaFolders
            .Include(f => f.SubFolders)
            .Include(f => f.MediaAssets)
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId);

        if (folder == null)
            throw new Exception("Folder not found");

        return new MediaFolderDto(
            folder.Id,
            folder.Name,
            folder.ParentFolderId,
            folder.CreatedAt,
            folder.SubFolders.Count,
            folder.MediaAssets.Count
        );
    }

    public async Task<MediaFolderDto> CreateFolderAsync(Guid tenantId, CreateMediaFolderRequest request)
    {
        var folder = new MediaFolder
        {
            Name = request.Name,
            ParentFolderId = request.ParentFolderId,
            TenantId = tenantId
        };

        _context.MediaFolders.Add(folder);
        await _context.SaveChangesAsync();

        return new MediaFolderDto(
            folder.Id,
            folder.Name,
            folder.ParentFolderId,
            folder.CreatedAt,
            0,
            0
        );
    }

    public async Task<MediaFolderDto> UpdateFolderAsync(Guid tenantId, Guid folderId, UpdateMediaFolderRequest request)
    {
        var folder = await _context.MediaFolders.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId);
        
        if (folder == null)
            throw new Exception("Folder not found");

        folder.Name = request.Name;
        folder.ParentFolderId = request.ParentFolderId;

        await _context.SaveChangesAsync();

        return new MediaFolderDto(
            folder.Id,
            folder.Name,
            folder.ParentFolderId,
            folder.CreatedAt,
            _context.MediaFolders.Count(sf => sf.ParentFolderId == folder.Id),
            _context.MediaAssets.Count(ma => ma.FolderId == folder.Id)
        );
    }

    public async Task DeleteFolderAsync(Guid tenantId, Guid folderId, bool force = false)
    {
        var folder = await _context.MediaFolders
            .Include(f => f.SubFolders)
            .Include(f => f.MediaAssets)
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId);

        if (folder == null)
            throw new Exception("Folder not found");

        if (!force && (folder.SubFolders.Any() || folder.MediaAssets.Any()))
            throw new Exception("NON_EMPTY_FOLDER"); // We will catch this exact message in the frontend

        if (force)
        {
            await DeleteFolderRecursiveAsync(tenantId, folder);
        }
        else
        {
            _context.MediaFolders.Remove(folder);
            await _context.SaveChangesAsync();
        }
    }

    private async Task DeleteFolderRecursiveAsync(Guid tenantId, MediaFolder folder)
    {
        // First delete all media assets via IMediaService to ensure files are deleted from storage
        var assets = await _context.MediaAssets
            .Where(a => a.FolderId == folder.Id && a.TenantId == tenantId)
            .ToListAsync();
            
        foreach (var asset in assets)
        {
            await _mediaService.DeleteAssetAsync(tenantId, asset.Id);
        }

        // Then recursively delete subfolders
        var subFolders = await _context.MediaFolders
            .Include(f => f.SubFolders)
            .Where(f => f.ParentFolderId == folder.Id && f.TenantId == tenantId)
            .ToListAsync();

        foreach (var subFolder in subFolders)
        {
            await DeleteFolderRecursiveAsync(tenantId, subFolder);
        }

        _context.MediaFolders.Remove(folder);
        await _context.SaveChangesAsync();
    }
}
