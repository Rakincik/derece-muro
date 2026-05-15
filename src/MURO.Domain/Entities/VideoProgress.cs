namespace MURO.Domain.Entities;

public class VideoProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MediaAssetId { get; set; }
    public int WatchedSeconds { get; set; }
    public int TotalSeconds { get; set; }
    public int LastPosition { get; set; }
    public int SkipCount { get; set; }    // Kaç kez ileri sarıldı
    public int ReplayCount { get; set; } // Kaç kez geri sarıldı
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public MediaAsset MediaAsset { get; set; } = null!;
}
