using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

/// <summary>
/// Bir paketin hangi gruba, hangi içerik moduyla erişim verdiğini tanımlar.
/// ContentMode: Online=sadece canlı, Offline=sadece video, Both=ikisi birden.
/// </summary>
public class PackageGroup
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public Guid GroupId { get; set; }

    /// <summary>
    /// Bu paket üzerinden o gruba hangi içerik tipine erişim verileceği.
    /// Online  = yalnızca canlı dersler
    /// Offline = yalnızca video kayıtlar
    /// Both    = canlı + video
    /// </summary>
    public CourseMode ContentMode { get; set; } = CourseMode.Both;

    // Navigation
    public Package Package { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
