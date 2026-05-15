using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class MediaFolderService : IMediaFolderService
{
    private readonly MuroDbContext _context;

    public MediaFolderService(MuroDbContext context)
    {
        _context = context;
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

    public async Task DeleteFolderAsync(Guid tenantId, Guid folderId)
    {
        var folder = await _context.MediaFolders
            .Include(f => f.SubFolders)
            .Include(f => f.MediaAssets)
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId);

        if (folder == null)
            throw new Exception("Folder not found");

        if (folder.SubFolders.Any() || folder.MediaAssets.Any())
            throw new Exception("Cannot delete a non-empty folder");

        _context.MediaFolders.Remove(folder);
        await _context.SaveChangesAsync();
    }
}
