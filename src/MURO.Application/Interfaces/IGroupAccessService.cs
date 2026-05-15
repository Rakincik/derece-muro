namespace MURO.Application.Interfaces;

/// <summary>
/// Grup ve Paket bazlı erişim kontrolü için merkezi yardımcı servis.
/// Öğrenci yalnızca aktif paketleri veya direkt grup atamaları üzerinden
/// erişebildiği içeriklere ulaşabilir.
/// </summary>
public interface IGroupAccessService
{
    /// <summary>
    /// Kullanıcının erişebildiği TÜM kurs ID'lerini döner.
    /// Kaynak 1: Aktif UserPackage → PackageGroup → CourseGroup
    /// Kaynak 2: Direkt GroupMember → CourseGroup
    /// Admin/Instructor ise tenantın tüm kurslarını döner.
    /// </summary>
    Task<HashSet<Guid>> GetAccessibleCourseIdsAsync(Guid tenantId, Guid userId);

    /// <summary>
    /// Video (Offline) içeriğe erişilebilir kurs ID'leri.
    /// ContentMode = Offline veya Both olan paket/grup atamaları.
    /// </summary>
    Task<HashSet<Guid>> GetVideoAccessibleCourseIdsAsync(Guid tenantId, Guid userId);

    /// <summary>
    /// Canlı ders (Online) içeriğe erişilebilir kurs ID'leri.
    /// ContentMode = Online veya Both olan paket/grup atamaları.
    /// </summary>
    Task<HashSet<Guid>> GetLiveAccessibleCourseIdsAsync(Guid tenantId, Guid userId);

    /// <summary>
    /// Belirli bir kursa herhangi bir içerik tipiyle erişim var mı?
    /// </summary>
    Task<bool> CanAccessCourseAsync(Guid tenantId, Guid userId, Guid courseId);

    /// <summary>
    /// Kullanıcının Admin veya Instructor rolünde olup olmadığını döner.
    /// True ise tüm filtreleme bypass edilir.
    /// </summary>
    Task<bool> IsStaffAsync(Guid userId);
}
