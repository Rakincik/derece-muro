using Microsoft.EntityFrameworkCore;
using MURO.Domain.Entities;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Persistence;

public class MuroDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public MuroDbContext(DbContextOptions<MuroDbContext> options, ICurrentUserService? currentUserService = null) 
        : base(options) 
    {
        _currentUserService = currentUserService;
    }

    // Core
    public DbSet<User> Users => Set<User>();
    // Groups
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    // Courses
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseGroup> CourseGroups => Set<CourseGroup>();
    public DbSet<CourseStudent> CourseStudents => Set<CourseStudent>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionRecording> SessionRecordings => Set<SessionRecording>();
    public DbSet<SessionAttendance> SessionAttendances => Set<SessionAttendance>();
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();

    // Media
    public DbSet<MediaFolder> MediaFolders => Set<MediaFolder>();
    public DbSet<CourseMedia> CourseMedias => Set<CourseMedia>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<VideoProgress> VideoProgresses => Set<VideoProgress>();
    public DbSet<VideoNote> VideoNotes => Set<VideoNote>();
    public DbSet<Podcast> Podcasts => Set<Podcast>();

    // Assignments
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();

    // Exams
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamAssignment> ExamAssignments => Set<ExamAssignment>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<ExamSubmissionQueue> ExamSubmissionQueues => Set<ExamSubmissionQueue>();
    public DbSet<StudentExamDraft> StudentExamDrafts => Set<StudentExamDraft>();

    // Calendar
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    // Communication
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Accounting
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Plan>        Plans        => Set<Plan>();

    // Packages (Satın alınan paketler)
    public DbSet<Package>     Packages     => Set<Package>();
    public DbSet<PackageGroup> PackageGroups => Set<PackageGroup>();
    public DbSet<UserPackage> UserPackages  => Set<UserPackage>();

    // Support
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<TelegramMessageMapping> TelegramMessageMappings => Set<TelegramMessageMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User — unique email and username
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Role).HasConversion<string>();
            entity.Property(u => u.StudentType).HasConversion<string>();
        });
// Group — self-referencing tree
        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasOne(g => g.Parent)
                  .WithMany(g => g.Children)
                  .HasForeignKey(g => g.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // GroupMember — composite unique
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasIndex(gm => new { gm.UserId, gm.GroupId }).IsUnique();
        });

        // CourseGroup — composite unique
        modelBuilder.Entity<CourseGroup>(entity =>
        {
            entity.HasIndex(cg => new { cg.CourseId, cg.GroupId }).IsUnique();
            entity.Property(cg => cg.Mode).HasConversion<string>();
        });

        // CourseStudent — composite unique
        modelBuilder.Entity<CourseStudent>(entity =>
        {
            entity.HasIndex(cs => new { cs.CourseId, cs.UserId }).IsUnique();
            entity.HasOne(cs => cs.Course).WithMany().HasForeignKey(cs => cs.CourseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cs => cs.User).WithMany().HasForeignKey(cs => cs.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Course
        modelBuilder.Entity<Course>(entity =>
        {
            entity.Property(c => c.Mode).HasConversion<string>();
            entity.Property(c => c.CourseType).HasConversion<string>();
            entity.HasOne(c => c.Instructor)
                  .WithMany()
                  .HasForeignKey(c => c.InstructorId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.Instructors)
                  .WithMany(u => u.InstructedCourses)
                  .UsingEntity<Dictionary<string, object>>(
                      "CourseInstructors",
                      j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                      j => j.HasOne<Course>().WithMany().HasForeignKey("CourseId").OnDelete(DeleteBehavior.Cascade));
        });

        // Session
        modelBuilder.Entity<Session>(entity =>
        {
            entity.Property(s => s.Status).HasConversion<string>();
        });

        // SessionRecording — one-to-one with Session
        modelBuilder.Entity<SessionRecording>(entity =>
        {
            entity.Property(sr => sr.Status).HasConversion<string>();
            entity.HasOne(sr => sr.Session)
                  .WithOne(s => s.Recording)
                  .HasForeignKey<SessionRecording>(sr => sr.SessionId);
        });

        // MediaAsset
        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.Property(ma => ma.Status).HasConversion<string>();
            entity.HasOne(ma => ma.Folder)
                  .WithMany(f => f.MediaAssets)
                  .HasForeignKey(ma => ma.FolderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // MediaFolder
        modelBuilder.Entity<MediaFolder>(entity =>
        {
            entity.HasOne(mf => mf.ParentFolder)
                  .WithMany(mf => mf.SubFolders)
                  .HasForeignKey(mf => mf.ParentFolderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // CourseMedia
        modelBuilder.Entity<CourseMedia>(entity =>
        {
            entity.HasIndex(cm => new { cm.CourseId, cm.MediaAssetId }).IsUnique();
            entity.HasIndex(cm => cm.OrderIndex);
            
            entity.HasOne(cm => cm.Course)
                  .WithMany(c => c.CourseMedias)
                  .HasForeignKey(cm => cm.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(cm => cm.MediaAsset)
                  .WithMany(ma => ma.CourseMedias)
                  .HasForeignKey(cm => cm.MediaAssetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // VideoProgress — composite unique
        modelBuilder.Entity<VideoProgress>(entity =>
        {
            entity.HasIndex(vp => new { vp.UserId, vp.MediaAssetId }).IsUnique();
        });

        // Question — two user references
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasOne(q => q.User)
                  .WithMany()
                  .HasForeignKey(q => q.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(q => q.Instructor)
                  .WithMany()
                  .HasForeignKey(q => q.InstructorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SupportMessage
        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasOne(sm => sm.Sender)
                  .WithMany()
                  .HasForeignKey(sm => sm.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(t => t.Amount).HasPrecision(18, 2);
        });

        // Notification — 🔧 Performans indexleri eklendi
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.Channel).HasConversion<string>();
            // Bildirim listesi sorgusu (userId + tenantId filtreleme)
            entity.HasIndex(n => new { n.UserId,  n.IsRead });
            // Admin: tenant bazlı gönderilmiş bildirimler (sıralama)
            entity.HasIndex(n => new { n.CreatedAt  });
        });

        // Podcast
        modelBuilder.Entity<Podcast>(entity =>
        {
            entity.Property(p => p.Status).HasConversion<string>();
        });

        // SessionAttendance — composite unique per user per session
        modelBuilder.Entity<SessionAttendance>(entity =>
        {
            entity.HasIndex(sa => new { sa.SessionId, sa.UserId }).IsUnique();
            // Öğrenci katılım geçmişi sorgusu
            entity.HasIndex(sa => new { sa.UserId  });
            entity.HasOne(sa => sa.Session).WithMany().HasForeignKey(sa => sa.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(sa => sa.User).WithMany().HasForeignKey(sa => sa.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // VideoNote — 🔧 index eklendi
        modelBuilder.Entity<VideoNote>(entity =>
        {
            entity.HasOne(vn => vn.User).WithMany().HasForeignKey(vn => vn.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(vn => vn.MediaAsset).WithMany().HasForeignKey(vn => vn.MediaAssetId).OnDelete(DeleteBehavior.Cascade);
            // Kullanıcının video notları sorgusu
            entity.HasIndex(vn => new { vn.UserId, vn.MediaAssetId });
        });

        // SecurityEvent — 🔧 IP blacklist sorgusu için index eklendi
        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.HasIndex(se => se.UserId);
            entity.HasIndex(se => new { se.EventType, se.CreatedAt });
            // IP blacklist middleware: IP + EventType + CreatedAt filtreleme
            entity.HasIndex(se => new { se.IpAddress, se.EventType, se.CreatedAt });
            entity.HasOne(se => se.User).WithMany().HasForeignKey(se => se.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Package sistemi
        modelBuilder.Entity<Package>(entity =>
        {
            entity.Property(p => p.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PackageGroup>(entity =>
        {
            entity.HasIndex(pg => new { pg.PackageId, pg.GroupId }).IsUnique();
            entity.Property(pg => pg.ContentMode).HasConversion<string>();
        });

        modelBuilder.Entity<UserPackage>(entity =>
        {
            entity.HasIndex(up => new { up.UserId, up.PackageId });
            entity.HasIndex(up => up.ExpiresAt);
            entity.HasIndex(up => up.OrderId);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => new { a.CreatedAt  });
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.EntityType);
            entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Exam — 🔧 tenant bazlı listeleme indexi
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasIndex(e => new { e.Status  });
            entity.HasIndex(e => new { e.CreatedAt  });
        });

        // ExamResult — 🔧 öğrenci sonuçları sorgusu
        modelBuilder.Entity<ExamResult>(entity =>
        {
            entity.HasIndex(er => new { er.ExamId, er.UserId });
            entity.HasIndex(er => er.UserId);
        });

        // ExamSubmissionQueue
        modelBuilder.Entity<ExamSubmissionQueue>(entity =>
        {
            entity.HasIndex(q => new { q.Status  });
        });

        // StudentExamDraft
        modelBuilder.Entity<StudentExamDraft>(entity =>
        {
            entity.HasIndex(d => new { d.ExamId, d.UserId  }).IsUnique();
        });

        // Course — 🔧 tenant bazlı listeleme
        modelBuilder.Entity<Course>(entity =>
        {
        });

        // SupportTicket — 🔧 tenant + status filtreleme
        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasIndex(st => new { st.Status  });
        });

        // TelegramMessageMapping
        modelBuilder.Entity<TelegramMessageMapping>(entity =>
        {
            entity.HasIndex(t => t.TelegramMessageId).IsUnique();
            entity.HasOne(t => t.SupportTicket)
                  .WithMany()
                  .HasForeignKey(t => t.SupportTicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // DeviceSession — 🔧 kullanıcı oturum sorgusu
        modelBuilder.Entity<DeviceSession>(entity =>
        {
            entity.HasIndex(ds => ds.UserId);
        });

        // Group — 🔧 tenant bazlı listeleme
        modelBuilder.Entity<Group>(entity =>
        {
        });

        // ─── Phase 2: Genişletilmiş Composite Index'ler ─────────────────────

        // Assignment — 🔧 tenant bazlı kurs filtreli listeleme + tarih sıralama
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasIndex(a => new { a.CourseId  });
            entity.HasIndex(a => new { a.DueDate  });
        });

        // AssignmentSubmission — 🔧 öğrenci teslim kontrolü (duplicate check)
        modelBuilder.Entity<AssignmentSubmission>(entity =>
        {
            entity.HasIndex(s => new { s.AssignmentId, s.UserId });
        });

        // Question — 🔧 soru filtreleme (status, eğitmen)
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasIndex(q => new { q.Status  });
            entity.HasIndex(q => new { q.InstructorId  });
        });

        // CalendarEvent — 🔧 takvim tarih aralığı + grup filtresi
        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.HasIndex(e => new { e.StartDate, e.EndDate  });
            entity.HasIndex(e => new { e.GroupId  });
        });

        // AuditLog — 🔧 denetim log sıralama + aksiyon filtresi
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => new { a.CreatedAt  });
            entity.HasIndex(a => new { a.Action  });
        });

        // MediaAsset — 🔧 medya listeleme + durum filtresi
        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.HasIndex(m => new { m.Status  });
            entity.HasIndex(m => new { m.CourseId  });
        });

        // VideoProgress — 🔧 kullanıcı video ilerleme sorgusu
        modelBuilder.Entity<VideoProgress>(entity =>
        {
            entity.HasIndex(vp => new { vp.UserId, vp.MediaAssetId });
        });

        // Session — 🔧 ders oturumları filtresi
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasIndex(s => new { s.CourseId, s.ScheduledStart });
        });


        // Transaction — 🔧 mali rapor sıralaması
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(t => t.TransactionDate);
        });

        // GroupMember — 🔧 grup üyeleri listeleme
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasIndex(gm => new { gm.GroupId, gm.Status });
            entity.HasIndex(gm => gm.UserId);
        });

        // Announcement — 🔧 duyuru sıralama (grup bazlı)
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasIndex(a => new { a.GroupId, a.CreatedAt });
        });

        // CourseMaterial — 🔧 ders materyalleri listeleme
        modelBuilder.Entity<CourseMaterial>(entity =>
        {
            entity.HasIndex(cm => cm.CourseId);
        });

        // ─── Phase 3: Son Eksik Index'ler ───────────────────────────────────

        // CourseGroup — 🔧 kurs-grup ilişkisi JOIN sorguları
        modelBuilder.Entity<CourseGroup>(entity =>
        {
            entity.HasIndex(cg => new { cg.CourseId, cg.GroupId });
        });

        // SessionRecording — 🔧 oturuma ait kayıt arama
        modelBuilder.Entity<SessionRecording>(entity =>
        {
            entity.HasIndex(sr => sr.SessionId);
        });

        // SupportMessage — 🔧 ticket'a ait mesajlar
        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasIndex(sm => sm.TicketId);
        });

        // UserPackage — 🔧 kullanıcının paketleri
        modelBuilder.Entity<UserPackage>(entity =>
        {
            entity.HasIndex(up => up.UserId);
        });

        // PackageGroup — 🔧 paket-grup ilişkisi
        modelBuilder.Entity<PackageGroup>(entity =>
        {
            entity.HasIndex(pg => pg.PackageId);
        });

        // ─── Soft Delete Global Query Filters (Restored for backward compatibility) ───
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Course>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Group>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Exam>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Session>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Assignment>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = base.SaveChanges();
        OnAfterSaveChanges(auditEntries);
        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        OnAfterSaveChanges(auditEntries);
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(cancellationToken);
        await OnAfterSaveChangesAsync(auditEntries);
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await OnAfterSaveChangesAsync(auditEntries);
        return result;
    }

    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.Entity is SecurityEvent ||
                entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            var auditEntry = new AuditEntry(entry)
            {
                TableName = entry.Entity.GetType().Name,
                UserId = _currentUserService?.UserId,
                UserName = _currentUserService?.UserName,
                IpAddress = _currentUserService?.IpAddress
            };

            auditEntries.Add(auditEntry);

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        return auditEntries;
    }

    private async Task OnAfterSaveChangesAsync(List<AuditEntry> auditEntries)
    {
        if (auditEntries == null || auditEntries.Count == 0)
            return;

        foreach (var auditEntry in auditEntries)
        {
            if (auditEntry.Entry.State == EntityState.Added)
            {
                foreach (var property in auditEntry.Entry.Properties)
                {
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[property.Metadata.Name] = property.CurrentValue;
                    }
                }
            }

            var log = auditEntry.ToAuditLog();
            AuditLogs.Add(log);
        }

        await base.SaveChangesAsync();
    }

    private void OnAfterSaveChanges(List<AuditEntry> auditEntries)
    {
        if (auditEntries == null || auditEntries.Count == 0)
            return;

        foreach (var auditEntry in auditEntries)
        {
            if (auditEntry.Entry.State == EntityState.Added)
            {
                foreach (var property in auditEntry.Entry.Properties)
                {
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[property.Metadata.Name] = property.CurrentValue;
                    }
                }
            }

            var log = auditEntry.ToAuditLog();
            AuditLogs.Add(log);
        }

        base.SaveChanges();
    }
}

