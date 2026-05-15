using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class SessionRecording
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? MediaAssetId { get; set; }
    public MediaStatus Status { get; set; } = MediaStatus.Processing;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Session Session { get; set; } = null!;
    public MediaAsset? MediaAsset { get; set; }
}
