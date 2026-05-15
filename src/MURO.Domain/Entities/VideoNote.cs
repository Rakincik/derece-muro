namespace MURO.Domain.Entities;

public class VideoNote
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MediaAssetId { get; set; }

    // Videonun hangi saniyesinde not alındı
    public int TimestampSeconds { get; set; }
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public MediaAsset MediaAsset { get; set; } = null!;
}
