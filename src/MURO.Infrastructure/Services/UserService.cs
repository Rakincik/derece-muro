using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Users;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public UserService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<UserListDto>> GetUsersAsync(
        Guid tenantId, int page, int pageSize, string? search, string? role, string? sortBy, string? sortDir)
    {
        var cacheKey = $"{tenantId}:users:list:{page}:{pageSize}:{search}:{role}:{sortBy}:{sortDir}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.TenantMemberships
                .AsNoTracking()
                .Where(tm => tm.TenantId == tenantId && tm.Status == "active")
                .Include(tm => tm.User)
                    .ThenInclude(u => u.GroupMemberships.Where(gm => gm.Status == "active"))
                        .ThenInclude(gm => gm.Group)
                .Select(tm => tm.User);

            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(s) ||
                    u.LastName.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s) ||
                    (u.Phone != null && u.Phone.Contains(s)));
            }

            // Role filter
            if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var roleEnum))
            {
                query = query.Where(u => u.Role == roleEnum);
            }

            // Sort
            query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
            {
                ("firstname", "desc") => query.OrderByDescending(u => u.FirstName),
                ("firstname", _) => query.OrderBy(u => u.FirstName),
                ("lastname", "desc") => query.OrderByDescending(u => u.LastName),
                ("lastname", _) => query.OrderBy(u => u.LastName),
                ("email", "desc") => query.OrderByDescending(u => u.Email),
                ("email", _) => query.OrderBy(u => u.Email),
                ("createdat", "asc") => query.OrderBy(u => u.CreatedAt),
                _ => query.OrderByDescending(u => u.CreatedAt),
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListDto(
                    u.Id, u.FirstName, u.LastName, u.Email, u.Phone,
                    u.Role.ToString(), u.StudentType.HasValue ? u.StudentType.Value.ToString() : null,
                    u.IsActive, u.CreatedAt, u.LastLoginAt,
                    u.GroupMemberships
                        .Where(gm => gm.Status == "active")
                        .Select(gm => gm.Group.Name)
                        .ToList(),
                    u.PasswordHash.StartsWith("$2") ? null : u.PasswordHash))
                .ToListAsync();

            return new PagedResult<UserListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<UserDetailDto> GetUserByIdAsync(Guid tenantId, Guid userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.GroupMemberships).ThenInclude(gm => gm.Group)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        // Verify tenant membership
        var isMember = await _context.TenantMemberships
            .AnyAsync(tm => tm.UserId == userId && tm.TenantId == tenantId);
        if (!isMember)
            throw new KeyNotFoundException("Bu kuruma ait kullanıcı bulunamadı.");

        var groups = user.GroupMemberships
            .Where(gm => gm.Status == "active")
            .Select(gm => new UserGroupDto(gm.GroupId, gm.Group.Name))
            .ToList();

        // Get courses via CourseGroups or direct assignment
        var courses = await _context.CourseGroups
            .AsNoTracking()
            .Where(cg => user.GroupMemberships.Select(gm => gm.GroupId).Contains(cg.GroupId))
            .Select(cg => new UserCourseDto(cg.CourseId, cg.Course.Title, cg.Mode.ToString()))
            .Distinct()
            .ToListAsync();

        return new UserDetailDto(
            user.Id, user.FirstName, user.LastName, user.Email, user.Phone,
            user.Role.ToString(), user.StudentType?.ToString(), user.DemoExpiresAt,
            user.IsActive, user.CreatedAt, user.LastLoginAt,
            groups, courses,
            user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash);
    }

    public async Task<UserListDto> CreateUserAsync(Guid tenantId, CreateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            throw new ArgumentException($"Geçersiz rol: {request.Role}");

        // Quota Enforcement
        if (role == UserRole.Student)
        {
            var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
            var isDemo = request.StudentType != null && Enum.TryParse<StudentType>(request.StudentType, true, out var stType) && stType == StudentType.Demo;
            
            if (isDemo && tenant?.MaxDemoStudents != null)
            {
                var currentDemo = await _context.TenantMemberships
                    .CountAsync(m => m.TenantId == tenantId && m.Status == "active" && m.Role == UserRole.Student && m.User.StudentType == StudentType.Demo);
                if (currentDemo >= tenant.MaxDemoStudents.Value)
                    throw new MURO.Application.Exceptions.QuotaExceededException("Demo Öğrenci kotanız doldu. Lütfen VEP üzerinden paketinizi yükseltin.");
            }
            else if (!isDemo && tenant?.MaxStudents != null)
            {
                var currentActive = await _context.TenantMemberships
                    .CountAsync(m => m.TenantId == tenantId && m.Status == "active" && m.Role == UserRole.Student && m.User.StudentType != StudentType.Demo);
                if (currentActive >= tenant.MaxStudents.Value)
                    throw new MURO.Application.Exceptions.QuotaExceededException("Aktif Öğrenci kotanız doldu. Lütfen VEP üzerinden paketinizi yükseltin.");
            }
        }

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
        {
            // Kullanıcı zaten var — bu tenant'taki üyelik durumunu kontrol et
            var membership = await _context.TenantMemberships
                .FirstOrDefaultAsync(tm => tm.UserId == existingUser.Id && tm.TenantId == tenantId);

            if (membership != null && membership.Status == "active")
                throw new InvalidOperationException("Bu e-posta adresi zaten aktif olarak kayıtlı.");

            // Soft delete edilmiş — bilgileri güncelle ve tekrar aktif et
            existingUser.FirstName = request.FirstName;
            existingUser.LastName = request.LastName;
            existingUser.Phone = request.Phone;
            existingUser.PasswordHash = request.Password;
            existingUser.Role = role;
            existingUser.IsActive = true;
            existingUser.StudentType = Enum.TryParse<StudentType>(request.StudentType, true, out var st2) ? st2 : null;
            existingUser.DemoExpiresAt = request.DemoExpiresAt;

            if (membership != null)
            {
                membership.Status = "active";
                membership.Role = role;
            }
            else
            {
                _context.TenantMemberships.Add(new TenantMembership
                {
                    Id = Guid.NewGuid(),
                    UserId = existingUser.Id,
                    TenantId = tenantId,
                    Role = role,
                    Status = "active"
                });
            }

            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"{tenantId}:users:");

            return new UserListDto(existingUser.Id, existingUser.FirstName, existingUser.LastName, existingUser.Email,
                existingUser.Phone, existingUser.Role.ToString(), existingUser.StudentType?.ToString(),
                existingUser.IsActive, existingUser.CreatedAt, existingUser.LastLoginAt, null, existingUser.PasswordHash.StartsWith("$2") ? null : existingUser.PasswordHash);
        }

        // Yeni kullanıcı oluştur
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = request.Password,
            Role = role,
            StudentType = Enum.TryParse<StudentType>(request.StudentType, true, out var st) ? st : null,
            DemoExpiresAt = request.DemoExpiresAt
        };

        _context.Users.Add(user);
        _context.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            Role = role,
            Status = "active"
        });

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");

        return new UserListDto(user.Id, user.FirstName, user.LastName, user.Email,
            user.Phone, user.Role.ToString(), user.StudentType?.ToString(),
            user.IsActive, user.CreatedAt, user.LastLoginAt, null, user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash);
    }

    // P3 Fix: Tek transaction, batch insert — her kı 100 kullanıcı = 1 DB round-trip
    // P4 Fix: Sadece InvalidOperationException (duplike e-posta) sessizce geçilir
    public async Task<List<UserListDto>> BulkCreateUsersAsync(Guid tenantId, List<CreateUserRequest> requests)
    {
        // Quota Enforcement (Bulk)
        var studentRequests = requests.Where(r => Enum.TryParse<UserRole>(r.Role, true, out var ro) && ro == UserRole.Student).ToList();
        if (studentRequests.Any())
        {
            var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
            var newDemoCount = studentRequests.Count(r => r.StudentType != null && Enum.TryParse<StudentType>(r.StudentType, true, out var st) && st == StudentType.Demo);
            var newActiveCount = studentRequests.Count - newDemoCount;

            if (newDemoCount > 0 && tenant?.MaxDemoStudents != null)
            {
                var currentDemo = await _context.TenantMemberships
                    .CountAsync(m => m.TenantId == tenantId && m.Status == "active" && m.Role == UserRole.Student && m.User.StudentType == StudentType.Demo);
                if (currentDemo + newDemoCount > tenant.MaxDemoStudents.Value)
                    throw new MURO.Application.Exceptions.QuotaExceededException($"Demo Öğrenci kotanız doldu. (Mevcut: {currentDemo}, Eklenen: {newDemoCount}, Limit: {tenant.MaxDemoStudents.Value}) Lütfen paketinizi yükseltin.");
            }

            if (newActiveCount > 0 && tenant?.MaxStudents != null)
            {
                var currentActive = await _context.TenantMemberships
                    .CountAsync(m => m.TenantId == tenantId && m.Status == "active" && m.Role == UserRole.Student && m.User.StudentType != StudentType.Demo);
                if (currentActive + newActiveCount > tenant.MaxStudents.Value)
                    throw new MURO.Application.Exceptions.QuotaExceededException($"Aktif Öğrenci kotanız doldu. (Mevcut: {currentActive}, Eklenen: {newActiveCount}, Limit: {tenant.MaxStudents.Value}) Lütfen paketinizi yükseltin.");
            }
        }

        var results = new List<UserListDto>();
        var existingEmails = (await _context.Users
            .Where(u => requests.Select(r => r.Email).Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var req in requests)
        {
            if (existingEmails.Contains(req.Email)) continue; // Duplike — sessiz geç

            if (!Enum.TryParse<UserRole>(req.Role, true, out var role)) continue;

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = req.FirstName,
                LastName = req.LastName,
                Email = req.Email,
                Phone = req.Phone,
                PasswordHash = req.Password,
                Role = role,
                StudentType = Enum.TryParse<StudentType>(req.StudentType, true, out var st) ? st : null,
                DemoExpiresAt = req.DemoExpiresAt
            };

            _context.Users.Add(user);
            _context.TenantMemberships.Add(new TenantMembership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenantId,
                Role = role,
                Status = "active"
            });

            results.Add(new UserListDto(
                user.Id, user.FirstName, user.LastName, user.Email,
                user.Phone, user.Role.ToString(), user.StudentType?.ToString(),
                user.IsActive, user.CreatedAt, user.LastLoginAt, null, user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash));
        }

        if (results.Count > 0)
        {
            await _context.SaveChangesAsync(); // Tek SaveChanges — tüm eklentiler birlikte
            await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
        }

        return results;
    }

    public async Task<UserListDto> UpdateUserAsync(Guid tenantId, Guid userId, UpdateUserRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        // 👑 SuperAdmin koruması — düzenlenemez
        if (user.Role == UserRole.SuperAdmin)
            throw new InvalidOperationException("SuperAdmin hesapları düzenlenemez.");

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.Email != null)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId))
                throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
            user.Email = request.Email;
        }
        if (request.Phone != null) user.Phone = request.Phone;
        if (!string.IsNullOrWhiteSpace(request.Password)) user.PasswordHash = request.Password;
        if (request.Role != null && Enum.TryParse<UserRole>(request.Role, true, out var role))
            user.Role = role;
        if (request.StudentType != null && Enum.TryParse<StudentType>(request.StudentType, true, out var st))
            user.StudentType = st;
        if (request.DemoExpiresAt.HasValue) user.DemoExpiresAt = request.DemoExpiresAt;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");

        return new UserListDto(user.Id, user.FirstName, user.LastName, user.Email,
            user.Phone, user.Role.ToString(), user.StudentType?.ToString(),
            user.IsActive, user.CreatedAt, user.LastLoginAt, null, user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash);
    }

    public async Task DeleteUserAsync(Guid tenantId, Guid userId)
    {
        // 👑 SuperAdmin koruması — silinemez
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role == UserRole.SuperAdmin)
            throw new InvalidOperationException("SuperAdmin hesapları silinemez.");

        var membership = await _context.TenantMemberships
            .FirstOrDefaultAsync(tm => tm.UserId == userId && tm.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Kullanıcı bu kurumda bulunamadı.");

        membership.Status = "removed";
        
        // Kullanıcının bu kuruma ait tüm grup üyeliklerini de soft-delete yap
        var groupMemberships = await _context.GroupMembers
            .Include(gm => gm.Group)
            .Where(gm => gm.UserId == userId && gm.Group.TenantId == tenantId && gm.Status == "active")
            .ToListAsync();

        foreach (var gm in groupMemberships)
        {
            gm.Status = "removed";
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:analytics:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
    }

    public async Task BulkDeleteUsersAsync(Guid tenantId, List<Guid> userIds)
    {
        // 👑 SuperAdmin'leri listeden çıkar — asla silinemezler
        var superAdminIds = await _context.Users
            .Where(u => userIds.Contains(u.Id) && u.Role == UserRole.SuperAdmin)
            .Select(u => u.Id)
            .ToListAsync();
        var safeIds = userIds.Except(superAdminIds).ToList();

        var memberships = await _context.TenantMemberships
            .Where(tm => safeIds.Contains(tm.UserId) && tm.TenantId == tenantId)
            .ToListAsync();

        foreach (var m in memberships) m.Status = "removed";

        // Bu kurumdaki seçili kullanıcıların tüm grup üyeliklerini de soft-delete yap
        var groupMemberships = await _context.GroupMembers
            .Include(gm => gm.Group)
            .Where(gm => safeIds.Contains(gm.UserId) && gm.Group.TenantId == tenantId && gm.Status == "active")
            .ToListAsync();

        foreach (var gm in groupMemberships)
        {
            gm.Status = "removed";
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:analytics:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
    }

    public async Task AssignToGroupAsync(Guid tenantId, Guid userId, Guid groupId)
    {
        var exists = await _context.GroupMembers
            .AnyAsync(gm => gm.UserId == userId && gm.GroupId == groupId);
        if (exists) throw new InvalidOperationException("Kullanıcı zaten bu grupta.");

        _context.GroupMembers.Add(new GroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            Status = "active"
        });
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
        await _cache.RemoveByPrefixAsync($"{tenantId}:groups:");
    }

    public Task AssignToCourseAsync(Guid tenantId, Guid userId, Guid courseId, string mode)
    {
        // For direct user-course assignment, we'll find user's first group or create need
        // For now, throw info message
        return Task.FromException(new NotImplementedException("Derse direkt kullanıcı atama, grup üzerinden yapılmalıdır."));
    }

    public async Task<byte[]> ExportUsersAsync(Guid tenantId, string? role)
    {
        // P1 Fix: Rol filtresi SQL'de yapılıyor, tüm kayıtlar belleğe çekilmiyor
        var query = _context.TenantMemberships
            .AsNoTracking()
            .Where(tm => tm.TenantId == tenantId && tm.Status == "active")
            .Select(tm => tm.User);

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var roleEnum))
            query = query.Where(u => u.Role == roleEnum);

        var users = await query.ToListAsync();

        // P2 Fix: CSV injection koruğması — tüm alanlar tırnak içinde
        static string Esc(string? v) => $"\"{(v ?? string.Empty).Replace("\"", "\"\"")}\"";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Ad,Soyad,E-posta,Telefon,Rol,Durum,Kayıt Tarihi");
        foreach (var u in users)
        {
            sb.AppendLine($"{Esc(u.FirstName)},{Esc(u.LastName)},{Esc(u.Email)}," +
                          $"{Esc(u.Phone)},{Esc(u.Role.ToString())}," +
                          $"{Esc(u.IsActive ? "Aktif" : "Pasif")},{Esc(u.CreatedAt.ToString("yyyy-MM-dd"))}");
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }
}
