using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Uygulama başlatıldığında SuperAdmin hesaplarını kontrol eder ve eksik olanları oluşturur.
/// SuperAdmin'ler platform sahipleridir — admin tarafından silinemez veya düzenlenemez.
/// Her tenant DB'sine otomatik eklenir.
/// </summary>
public static class SuperAdminSeeder
{
    private static readonly List<SuperAdminDefinition> SuperAdmins = new()
    {
        new("Rüstem", "Akıncık", "rustemakincik@on7yazilim.com", "ra7.on7yazilim.com"),
        new("Volkan", "Çetin", "volkancetin@on7yazilim.com", "Volkan1906."),
        new("Osman", "Badıllı", "osmanbadillli@on7yazilim.com", "Badilli.on7yazilim"),
        new("İlhan", "Çetin", "ilhancetin@on7yazilim.com", "İlsex.1907"),
    };

    /// <summary>
    /// Master DB'de SuperAdmin'leri kontrol et ve eksik olanları oluştur.
    /// Tüm tenant'lara TenantMembership olarak da ekle.
    /// Program.cs → app başlarken çağrılır.
    /// </summary>
    public static async Task SeedAsync(MuroDbContext db, ILogger? logger = null)
    {
        var created = 0;

        // Mevcut tüm tenant'ları al — SuperAdmin'leri hepsine ekleyeceğiz
        var tenants = await db.Tenants.Where(t => t.IsActive).ToListAsync();

        foreach (var def in SuperAdmins)
        {
            var user = await db.Users
                .Include(u => u.TenantMemberships)
                .FirstOrDefaultAsync(u => u.Email == def.Email);

            if (user != null)
            {
                // Mevcut kullanıcıyı SuperAdmin yap (rolü değiştiyse geri al)
                if (user.Role != UserRole.SuperAdmin)
                {
                    user.Role = UserRole.SuperAdmin;
                    logger?.LogWarning("SuperAdmin rolü geri yüklendi: {Email}", def.Email);
                }

                // Mevcut şifre hashliyse veya değişmişse plaintext ile güncelle
                if (user.PasswordHash != def.Password)
                {
                    user.PasswordHash = def.Password;
                }

                // Eksik tenant membership'leri ekle
                foreach (var tenant in tenants)
                {
                    var hasMembership = user.TenantMemberships.Any(m => m.TenantId == tenant.Id);
                    if (!hasMembership)
                    {
                        db.TenantMemberships.Add(new TenantMembership
                        {
                            UserId = user.Id,
                            TenantId = tenant.Id,
                            Role = UserRole.SuperAdmin,
                            Status = "active",
                            JoinedAt = DateTime.UtcNow
                        });
                        logger?.LogInformation("SuperAdmin {Email} → {Tenant} tenant'a eklendi.", def.Email, tenant.Name);
                    }
                }
                continue;
            }

            // Yeni SuperAdmin oluştur
            var superAdmin = new User
            {
                Id = Guid.NewGuid(),
                FirstName = def.FirstName,
                LastName = def.LastName,
                Email = def.Email,
                PasswordHash = def.Password,
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(superAdmin);

            // Tüm tenant'lara ekle
            foreach (var tenant in tenants)
            {
                db.TenantMemberships.Add(new TenantMembership
                {
                    UserId = superAdmin.Id,
                    TenantId = tenant.Id,
                    Role = UserRole.SuperAdmin,
                    Status = "active",
                    JoinedAt = DateTime.UtcNow
                });
            }

            created++;
            logger?.LogInformation("SuperAdmin oluşturuldu: {Email} ({Count} tenant'a eklendi)", def.Email, tenants.Count);
        }

        if (created > 0 || db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            if (created > 0)
                logger?.LogInformation("{Count} yeni SuperAdmin hesabı oluşturuldu.", created);
        }
    }

    private record SuperAdminDefinition(string FirstName, string LastName, string Email, string Password);
}
