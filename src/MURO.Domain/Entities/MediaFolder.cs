using System;
using System.Collections.Generic;

namespace MURO.Domain.Entities;

public class MediaFolder
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public MediaFolder? ParentFolder { get; set; }
    public ICollection<MediaFolder> SubFolders { get; set; } = new List<MediaFolder>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
}
