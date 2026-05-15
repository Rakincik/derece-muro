using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Groups;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class GroupService : IGroupService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public GroupService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<GroupListDto>> GetGroupsAsync(Guid tenantId, int page, int pageSize, string? search)
    {
        var cacheKey = $"{tenantId}:groups:list:{page}:{pageSize}:{search}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Groups.AsNoTracking()
                .Where(g => g.TenantId == tenantId)
                .Include(g => g.Members)
                .Include(g => g.Parent)
                .Include(g => g.CourseGroups)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(g => g.Name.ToLower().Contains(s));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderBy(g => g.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => new GroupListDto(
                    g.Id, g.Name, g.Description,
                    g.ParentId, g.Parent != null ? g.Parent.Name : null,
                    g.Color, g.EducationType,
                    g.Members.Count(m => m.Status == "active"),
                    g.CourseGroups.Count,
                    g.CreatedAt))
                .ToListAsync();

            return new PagedResult<GroupListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<List<GroupTreeDto>> GetGroupTreeAsync(Guid tenantId)
    {
        var cacheKey = $"{tenantId}:groups:tree";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var allGroups = await _context.Groups.AsNoTracking()
                .Where(g => g.TenantId == tenantId)
                .Include(g => g.Members)
                .Select(g => new { g.Id, g.Name, g.ParentId, MemberCount = g.Members.Count(m => m.Status == "active") })
                .ToListAsync();

            List<GroupTreeDto> BuildChildren(Guid? parentId) =>
                allGroups
                    .Where(g => g.ParentId == parentId)
                    .Select(g => new GroupTreeDto(g.Id, g.Name, g.MemberCount, BuildChildren(g.Id)))
                    .ToList();

            return BuildChildren(null);
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<GroupDetailDto> GetGroupByIdAsync(Guid tenantId, Guid groupId)
    {
        var cacheKey = $"{tenantId}:groups:detail:{groupId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var group = await _context.Groups.AsNoTracking()
                .Where(g => g.Id == groupId && g.TenantId == tenantId)
                .Include(g => g.Parent)
                .Include(g => g.Members).ThenInclude(m => m.User)
                .Include(g => g.CourseGroups).ThenInclude(cg => cg.Course)
                .Include(g => g.Children)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Grup bulunamadı.");

            return new GroupDetailDto(
                group.Id, group.Name, group.Description,
                group.ParentId, group.Parent?.Name,
                group.Color, group.EducationType,
                group.Members.Count(m => m.Status == "active"),
                group.CourseGroups.Count,
                group.Members
                    .Where(m => m.Status == "active")
                    .Select(m => new GroupMemberDto(
                        m.UserId, $"{m.User.FirstName} {m.User.LastName}",
                        m.User.Email, m.Role.ToString(), m.AddedAt))
                    .ToList(),
                group.CourseGroups
                    .Select(cg => new GroupCourseDto(cg.CourseId, cg.Course.Title, cg.Mode.ToString()))
                    .ToList(),
                group.Children
                    .Select(c => new GroupChildDto(c.Id, c.Name, c.Members.Count))
                    .ToList(),
                group.CreatedAt);
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<GroupListDto> CreateGroupAsync(Guid tenantId, CreateGroupRequest request)
    {
        if (request.ParentGroupId.HasValue)
        {
            var parentExists = await _context.Groups.AnyAsync(g => g.Id == request.ParentGroupId && g.TenantId == tenantId);
            if (!parentExists) throw new KeyNotFoundException("Üst grup bulunamadı.");
        }

        var group = new Group
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Name = request.Name, Description = request.Description,
            Color = request.Color, EducationType = request.EducationType,
            ParentId = request.ParentGroupId
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");

        return new GroupListDto(group.Id, group.Name, group.Description, group.ParentId, null, group.Color, group.EducationType, 0, 0, group.CreatedAt);
    }

    public async Task<GroupListDto> UpdateGroupAsync(Guid tenantId, Guid groupId, UpdateGroupRequest request)
    {
        var group = await _context.Groups
            .Include(g => g.Members)
            .Include(g => g.CourseGroups)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Grup bulunamadı.");

        if (request.Name != null) group.Name = request.Name;
        if (request.Description != null) group.Description = request.Description;
        if (request.Color != null) group.Color = request.Color;
        if (request.EducationType != null) group.EducationType = request.EducationType;
        if (request.ParentGroupId.HasValue)
        {
            if (request.ParentGroupId == groupId)
                throw new ArgumentException("Grup kendi kendisinin alt grubu olamaz.");
            group.ParentId = request.ParentGroupId;
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");

        return new GroupListDto(group.Id, group.Name, group.Description, group.ParentId, null, group.Color, group.EducationType,
            group.Members.Count(m => m.Status == "active"), group.CourseGroups.Count, group.CreatedAt);
    }

    public async Task DeleteGroupAsync(Guid tenantId, Guid groupId)
    {
        var group = await _context.Groups.Include(g => g.Children)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Grup bulunamadı.");

        if (group.Children.Any())
            throw new InvalidOperationException("Alt grupları olan bir grup silinemez. Önce alt grupları silin.");

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
    }

    public async Task ForceDeleteGroupAsync(Guid tenantId, Guid groupId)
    {
        var group = await _context.Groups
            .Include(g => g.Children)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Grup bulunamadı.");

        // Recursive delete children helper
        async Task DeleteChildren(Group g)
        {
            var children = await _context.Groups.Include(c => c.Children).Where(c => c.ParentId == g.Id).ToListAsync();
            foreach (var child in children)
            {
                await DeleteChildren(child);
                _context.Groups.Remove(child);
            }
        }

        await DeleteChildren(group);
        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
        
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
    }

    public async Task<GroupListDto> CloneGroupAsync(Guid tenantId, Guid groupId, string newName, bool copyMembers, bool copyCourses)
    {
        var group = await _context.Groups
            .Include(g => g.Members)
            .Include(g => g.CourseGroups)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Klonlanacak grup bulunamadı.");

        var newGroup = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = newName,
            Description = group.Description,
            Color = group.Color,
            EducationType = group.EducationType,
            ParentId = group.ParentId
        };

        _context.Groups.Add(newGroup);

        if (copyMembers)
        {
            var activeMembers = group.Members.Where(m => m.Status == "active");
            foreach (var member in activeMembers)
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    Id = Guid.NewGuid(),
                    GroupId = newGroup.Id,
                    UserId = member.UserId,
                    Role = member.Role,
                    Status = "active"
                });
            }
        }

        if (copyCourses)
        {
            foreach (var cg in group.CourseGroups)
            {
                _context.CourseGroups.Add(new CourseGroup
                {
                    Id = Guid.NewGuid(),
                    GroupId = newGroup.Id,
                    CourseId = cg.CourseId,
                    Mode = cg.Mode
                });
            }
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");

        var finalMemberCount = copyMembers ? group.Members.Count(m => m.Status == "active") : 0;
        var finalCourseCount = copyCourses ? group.CourseGroups.Count : 0;

        return new GroupListDto(newGroup.Id, newGroup.Name, newGroup.Description, newGroup.ParentId, null, newGroup.Color, newGroup.EducationType, finalMemberCount, finalCourseCount, newGroup.CreatedAt);
    }

    public async Task AddMembersAsync(Guid tenantId, Guid groupId, List<Guid> userIds)
    {
        var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId && g.TenantId == tenantId);
        if (!groupExists) throw new KeyNotFoundException("Grup bulunamadı.");

        var existingMemberRecords = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId && userIds.Contains(gm.UserId))
            .ToListAsync();

        foreach (var record in existingMemberRecords)
        {
            if (record.Status != "active")
            {
                record.Status = "active";
            }
        }

        var existingUserIds = existingMemberRecords.Select(gm => gm.UserId).ToList();

        var newMembers = userIds.Except(existingUserIds)
            .Select(userId => new GroupMember
            {
                Id = Guid.NewGuid(), GroupId = groupId, UserId = userId,
                Role = Domain.Enums.UserRole.Student, Status = "active"
            });

        _context.GroupMembers.AddRange(newMembers);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
    }

    public async Task RemoveMemberAsync(Guid tenantId, Guid groupId, Guid userId)
    {
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId)
            ?? throw new KeyNotFoundException("Üye bulunamadı.");

        member.Status = "removed";
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
    }

    public async Task MoveMembersAsync(Guid tenantId, Guid fromGroupId, Guid toGroupId, List<Guid> userIds)
    {
        var toGroupExists = await _context.Groups.AnyAsync(g => g.Id == toGroupId && g.TenantId == tenantId);
        if (!toGroupExists) throw new KeyNotFoundException("Hedef grup bulunamadı.");

        var membersToMove = await _context.GroupMembers
            .Where(gm => gm.GroupId == fromGroupId && userIds.Contains(gm.UserId) && gm.Status == "active")
            .ToListAsync();

        if (!membersToMove.Any()) return;

        // Kaynak gruptan çıkar
        foreach (var m in membersToMove) m.Status = "removed";

        // Hedef gruba ekle (zaten orada olmayanları veya silinmişleri aktif et)
        var targetMemberRecords = await _context.GroupMembers
            .Where(gm => gm.GroupId == toGroupId && userIds.Contains(gm.UserId))
            .ToListAsync();

        foreach (var record in targetMemberRecords)
        {
            if (record.Status != "active")
            {
                record.Status = "active";
            }
        }

        var alreadyInTarget = targetMemberRecords.Select(gm => gm.UserId).ToList();

        var toAdd = membersToMove
            .Where(m => !alreadyInTarget.Contains(m.UserId))
            .Select(m => new GroupMember
            {
                Id = Guid.NewGuid(), GroupId = toGroupId,
                UserId = m.UserId, Role = m.Role, Status = "active"
            });

        _context.GroupMembers.AddRange(toAdd);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
    }
}
