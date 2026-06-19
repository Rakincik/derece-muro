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
                    u.PasswordHash.StartsWith("$2") ? null : u.PasswordHash,
                    u.TcNo))
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
            user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash,
            user.TcNo);
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

        var isStudent = role == UserRole.Student;
        var cleanedPhone = CleanPhoneNumber(request.Phone);
        
        string email = request.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            var baseUsername = ToEnglishUsername(request.FirstName, request.LastName);
            email = baseUsername;
            int suffix = 1;
            while (await _context.Users.AnyAsync(u => u.Email == email))
            {
                email = $"{baseUsername}{suffix}";
                suffix++;
            }
        }
        else
        {
            email = email.Trim();
        }

        string password = request.Password;
        if (isStudent)
        {
            var lastTwo = cleanedPhone != null && cleanedPhone.Length >= 2 
                ? cleanedPhone.Substring(cleanedPhone.Length - 2) 
                : "00";
            password = $"{request.TcNo}.{lastTwo}";
        }

        var tcCheck = request.TcNo?.Trim();
        var existingUser = await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => 
                u.Email == email || 
                (!string.IsNullOrEmpty(tcCheck) && u.TcNo == tcCheck) ||
                (!string.IsNullOrEmpty(cleanedPhone) && u.Phone == cleanedPhone));

        if (existingUser != null)
        {
            // Kullanıcı zaten var — bu tenant'taki üyelik durumunu kontrol et
            var membership = await _context.TenantMemberships
                .FirstOrDefaultAsync(tm => tm.UserId == existingUser.Id && tm.TenantId == tenantId);

            if (membership != null && membership.Status == "active")
                throw new InvalidOperationException("Bu TC Kimlik numarası, Telefon veya E-posta adresi ile aktif bir kullanıcı zaten kayıtlı.");

            // Soft delete edilmiş — bilgileri güncelle ve tekrar aktif et
            existingUser.FirstName = request.FirstName;
            existingUser.LastName = request.LastName;
            if (!string.IsNullOrEmpty(cleanedPhone)) existingUser.Phone = cleanedPhone;
            existingUser.PasswordHash = password;
            existingUser.Role = role;
            existingUser.IsActive = true;
            existingUser.IsDeleted = false; // Restore user visibility globally
            existingUser.StudentType = Enum.TryParse<StudentType>(request.StudentType, true, out var st2) ? st2 : null;
            existingUser.DemoExpiresAt = request.DemoExpiresAt;
            if (!string.IsNullOrEmpty(tcCheck)) existingUser.TcNo = tcCheck;

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
                existingUser.IsActive, existingUser.CreatedAt, existingUser.LastLoginAt, null, existingUser.PasswordHash.StartsWith("$2") ? null : existingUser.PasswordHash,
                existingUser.TcNo);
        }

        // Yeni kullanıcı oluştur
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = email,
            Phone = cleanedPhone,
            PasswordHash = password,
            Role = role,
            StudentType = Enum.TryParse<StudentType>(request.StudentType, true, out var st) ? st : null,
            DemoExpiresAt = request.DemoExpiresAt,
            TcNo = request.TcNo
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
            user.IsActive, user.CreatedAt, user.LastLoginAt, null, user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash,
            user.TcNo);
    }

    // P3 Fix: Tek transaction, batch insert — her kı 100 kullanıcı = 1 DB round-trip
    // P4 Fix: Sadece InvalidOperationException (duplike e-posta) sessizce geçilir
    public async Task<BulkImportResultDto> BulkCreateUsersAsync(Guid tenantId, List<CreateUserRequest> requests)
    {
        var importResult = new BulkImportResultDto { TotalAttempted = requests.Count };

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
        
        var requestTcs = requests.Where(r => !string.IsNullOrEmpty(r.TcNo)).Select(r => r.TcNo.Trim()).ToList();
        var requestPhones = requests.Where(r => !string.IsNullOrEmpty(r.Phone)).Select(r => CleanPhoneNumber(r.Phone)).Where(p => p != null).ToList();
        var requestEmails = requests.Where(r => !string.IsNullOrEmpty(r.Email)).Select(r => r.Email.Trim()).ToList();

        var existingUsersMatch = await _context.Users.IgnoreQueryFilters()
            .Include(u => u.TenantMemberships.Where(tm => tm.TenantId == tenantId))
            .Where(u => requestEmails.Contains(u.Email) || 
                       (!string.IsNullOrEmpty(u.TcNo) && requestTcs.Contains(u.TcNo)) || 
                       (!string.IsNullOrEmpty(u.Phone) && requestPhones.Contains(u.Phone)))
            .ToListAsync();

        var existingUsersByTc = existingUsersMatch.Where(u => !string.IsNullOrEmpty(u.TcNo)).ToDictionary(u => u.TcNo, StringComparer.OrdinalIgnoreCase);
        var existingUsersByPhone = existingUsersMatch.Where(u => !string.IsNullOrEmpty(u.Phone)).ToDictionary(u => u.Phone, StringComparer.OrdinalIgnoreCase);
        var existingUsersByEmail = existingUsersMatch.ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);

        var generatedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generatedTcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generatedPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track emails across the whole db for generation
        var allDbEmails = (await _context.Users.IgnoreQueryFilters().Select(u => u.Email).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var req in requests)
        {
            if (!Enum.TryParse<UserRole>(req.Role, true, out var role)) 
            {
                importResult.FailedCount++;
                importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = req.Email ?? "", Status = "Başarısız", Reason = "Geçersiz Rol" });
                continue;
            }
            var isStudent = role == UserRole.Student;

            var tc = req.TcNo?.Trim();
            var cleanedPhone = CleanPhoneNumber(req.Phone);
            string email = req.Email?.Trim();

            User? existingUser = null;

            if (!string.IsNullOrEmpty(tc) && existingUsersByTc.TryGetValue(tc, out var userByTc)) existingUser = userByTc;
            else if (!string.IsNullOrEmpty(cleanedPhone) && existingUsersByPhone.TryGetValue(cleanedPhone, out var userByPhone)) existingUser = userByPhone;
            else if (!string.IsNullOrEmpty(email) && existingUsersByEmail.TryGetValue(email, out var userByEmail)) existingUser = userByEmail;

            // Self-duplicate checks in the current batch
            if (!string.IsNullOrEmpty(tc) && existingUser == null)
            {
                if (generatedTcs.Contains(tc))
                {
                    importResult.FailedCount++;
                    importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = req.Email ?? "", Status = "Başarısız", Reason = "Bu excel listesinde aynı TC mükerrer girilmiş" });
                    continue;
                }
                generatedTcs.Add(tc);
            }

            if (!string.IsNullOrEmpty(cleanedPhone) && existingUser == null)
            {
                if (generatedPhones.Contains(cleanedPhone))
                {
                    importResult.FailedCount++;
                    importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = req.Email ?? "", Status = "Başarısız", Reason = "Bu excel listesinde aynı Telefon mükerrer girilmiş" });
                    continue;
                }
                generatedPhones.Add(cleanedPhone);
            }

            if (string.IsNullOrWhiteSpace(email) && existingUser == null)
            {
                var baseUsername = ToEnglishUsername(req.FirstName, req.LastName);
                email = baseUsername;
                int suffix = 1;
                while (allDbEmails.Contains(email) || generatedEmails.Contains(email))
                {
                    email = $"{baseUsername}{suffix}";
                    suffix++;
                }
                generatedEmails.Add(email);
            }
            else if (!string.IsNullOrEmpty(email) && existingUser == null)
            {
                if (generatedEmails.Contains(email)) 
                {
                    importResult.FailedCount++;
                    importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = email, Status = "Başarısız", Reason = "Bu excel listesinde aynı E-posta mükerrer girilmiş" });
                    continue;
                }
                generatedEmails.Add(email);
            }

            string password = req.Password;
            if (isStudent && string.IsNullOrEmpty(password))
            {
                var lastTwo = cleanedPhone != null && cleanedPhone.Length >= 2 
                    ? cleanedPhone.Substring(cleanedPhone.Length - 2) 
                    : "00";
                password = $"{req.TcNo}.{lastTwo}";
            }

            if (existingUser != null)
            {
                var membership = existingUser.TenantMemberships.FirstOrDefault();
                if (membership != null && membership.Status == "active")
                {
                    importResult.FailedCount++;
                    importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = email ?? existingUser.Email, Status = "Başarısız", Reason = "Bu kullanıcı (TC, Telefon veya E-posta) zaten aktif olarak kayıtlı." });
                    continue;
                }

                // Reactivate
                existingUser.FirstName = req.FirstName;
                existingUser.LastName = req.LastName;
                if (!string.IsNullOrEmpty(cleanedPhone)) existingUser.Phone = cleanedPhone;
                if (!string.IsNullOrEmpty(password)) existingUser.PasswordHash = password;
                existingUser.Role = role;
                existingUser.IsActive = true;
                existingUser.IsDeleted = false; // Restore
                existingUser.StudentType = Enum.TryParse<StudentType>(req.StudentType, true, out var st2) ? st2 : null;
                if (!string.IsNullOrEmpty(tc)) existingUser.TcNo = tc;
                
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
                
                importResult.ImportedCount++;
                importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = existingUser.Email, Status = "Başarılı", Reason = "Eski kayıt başarıyla aktifleştirildi" });
                
                results.Add(new UserListDto(
                    existingUser.Id, existingUser.FirstName, existingUser.LastName, existingUser.Email,
                    existingUser.Phone, existingUser.Role.ToString(), existingUser.StudentType?.ToString(),
                    existingUser.IsActive, existingUser.CreatedAt, existingUser.LastLoginAt, null, existingUser.PasswordHash.StartsWith("$2") ? null : existingUser.PasswordHash,
                    existingUser.TcNo));
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = req.FirstName,
                LastName = req.LastName,
                Email = email,
                Phone = cleanedPhone,
                PasswordHash = password,
                Role = role,
                StudentType = Enum.TryParse<StudentType>(req.StudentType, true, out var st) ? st : null,
                DemoExpiresAt = req.DemoExpiresAt,
                TcNo = tc
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

            importResult.ImportedCount++;
            importResult.Details.Add(new BulkImportItemResultDto { FirstName = req.FirstName, LastName = req.LastName, Email = email, Status = "Başarılı", Reason = "Başarıyla eklendi" });

            results.Add(new UserListDto(
                user.Id, user.FirstName, user.LastName, user.Email,
                user.Phone, user.Role.ToString(), user.StudentType?.ToString(),
                user.IsActive, user.CreatedAt, user.LastLoginAt, null, user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash,
                user.TcNo));
        }

        if (results.Count > 0)
        {
            await _context.SaveChangesAsync(); // Tek SaveChanges — tüm eklentiler birlikte
            await _cache.RemoveByPrefixAsync($"{tenantId}:users:");
        }

        return importResult;
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
                throw new InvalidOperationException("Bu kullanıcı adı veya e-posta adresi zaten kullanılıyor.");
            user.Email = request.Email;
        }
        if (request.Phone != null) user.Phone = CleanPhoneNumber(request.Phone);
        if (!string.IsNullOrWhiteSpace(request.Password)) user.PasswordHash = request.Password;
        if (request.Role != null && Enum.TryParse<UserRole>(request.Role, true, out var role))
            user.Role = role;
        if (request.StudentType != null && Enum.TryParse<StudentType>(request.StudentType, true, out var st))
            user.StudentType = st;
        if (request.DemoExpiresAt.HasValue) user.DemoExpiresAt = request.DemoExpiresAt;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        if (request.TcNo != null) user.TcNo = request.TcNo;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:users:");

        return new UserListDto(user.Id, user.FirstName, user.LastName, user.Email,
            user.Phone, user.Role.ToString(), user.StudentType?.ToString(),
            user.IsActive, user.CreatedAt, user.LastLoginAt, null, user.PasswordHash.StartsWith("$2") ? null : user.PasswordHash,
            user.TcNo);
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
        sb.AppendLine("Ad,Soyad,Kullanıcı Adı,TC,Telefon,Rol,Durum,Kayıt Tarihi");
        foreach (var u in users)
        {
            sb.AppendLine($"{Esc(u.FirstName)},{Esc(u.LastName)},{Esc(u.Email)},{Esc(u.TcNo)}," +
                          $"{Esc(u.Phone)},{Esc(u.Role.ToString())}," +
                          $"{Esc(u.IsActive ? "Aktif" : "Pasif")},{Esc(u.CreatedAt.ToString("yyyy-MM-dd"))}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        return bom.Concat(bytes).ToArray();
    }

    public static string ToEnglishUsername(string firstName, string lastName)
    {
        var combined = $"{firstName}{lastName}".Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in combined)
        {
            switch (c)
            {
                case 'ç': sb.Append('c'); break;
                case 'ğ': sb.Append('g'); break;
                case 'ı': sb.Append('i'); break;
                case 'ö': sb.Append('o'); break;
                case 'ş': sb.Append('s'); break;
                case 'ü': sb.Append('u'); break;
                case ' ': break;
                default:
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public static string? CleanPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        
        // Take only the integer part before any decimal point/comma (handles Excel number formatting like "5321234567.0")
        var mainPart = phone.Split('.', ',')[0];
        
        var digits = new string(mainPart.Where(char.IsDigit).ToArray());
        
        // Strip all leading zeros
        while (digits.StartsWith("0"))
        {
            digits = digits.Substring(1);
        }
        
        // If it starts with country code "90" and has 12 digits (e.g. 905321234567), strip "90"
        if (digits.Length == 12 && digits.StartsWith("90"))
        {
            digits = digits.Substring(2);
        }
        
        return digits;
    }
}
