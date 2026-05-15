using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Gerçek PostgreSQL (Docker) ile veri bütünlüğü testleri.
/// InMemory DB'nin yakalayamadığı constraint ve transaction senaryoları.
/// </summary>
[Trait("Category", "Postgres")]
public class PostgresDataIntegrityTests : PostgresTestBase
{
    // ────────────────────────────────────────────────────────────────────────
    // 1. Unique email constraint → duplicate hata
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateEmail_ShouldThrowDbUpdateException()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "T", Code = "t", CreatedAt = DateTime.UtcNow };
        Db.Tenants.Add(tenant);
        await Db.SaveChangesAsync();

        var user1 = new User
        {
            Id = Guid.NewGuid(), Email = "duplicate@test.com",
            FirstName = "A", LastName = "B", PasswordHash = "h",
            Role = UserRole.Student, IsActive = true
        };
        Db.Users.Add(user1);
        await Db.SaveChangesAsync();

        var user2 = new User
        {
            Id = Guid.NewGuid(), Email = "duplicate@test.com", // Aynı email
            FirstName = "C", LastName = "D", PasswordHash = "h",
            Role = UserRole.Student, IsActive = true
        };
        Db.Users.Add(user2);

        Func<Task> act = () => Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. FK cascade: Kurs silinince sessionlar da silinmeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCourse_ShouldCascadeDeleteSessions()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "T", Code = "cas", CreatedAt = DateTime.UtcNow };
        var courseId = Guid.NewGuid();
        Db.Tenants.Add(tenant);
        Db.Courses.Add(new Course { Id = courseId, TenantId = tenant.Id, Title = "Silinecek Ders" });
        Db.Sessions.Add(new Session { Id = Guid.NewGuid(), CourseId = courseId, Title = "S1", Status = SessionStatus.Scheduled });
        Db.Sessions.Add(new Session { Id = Guid.NewGuid(), CourseId = courseId, Title = "S2", Status = SessionStatus.Scheduled });
        await Db.SaveChangesAsync();

        var course = await Db.Courses.FindAsync(courseId);
        Db.Courses.Remove(course!);
        await Db.SaveChangesAsync();

        var sessions = await Db.Sessions.Where(s => s.CourseId == courseId).ToListAsync();
        sessions.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Migration doğru uygulanmış mı? → tablo sayısı kontrolü
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MigrationApplied_ShouldHaveAllTables()
    {
        var tables = await Db.Database
            .SqlQueryRaw<string>("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        // En az bu tablolar olmalı
        tables.Should().Contain("Users");
        tables.Should().Contain("Tenants");
        tables.Should().Contain("Courses");
        tables.Should().Contain("Sessions");
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. Büyük veri — 500 kurs insert performansı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkInsert_500Courses_ShouldCompleteInTime()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Bulk", Code = "bulk", CreatedAt = DateTime.UtcNow };
        Db.Tenants.Add(tenant);

        for (int i = 0; i < 500; i++)
        {
            Db.Courses.Add(new Course
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                Title = $"Kurs {i}", IsPublished = true
            });
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Db.SaveChangesAsync();
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 saniyenin altında
        var count = await Db.Courses.CountAsync(c => c.TenantId == tenant.Id);
        count.Should().Be(500);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. Tenant silince bağlı membership'ler de temizlenmeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTenant_ShouldCascadeMemberships()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        Db.Tenants.Add(new Tenant { Id = tenantId, Name = "Del", Code = "del", CreatedAt = DateTime.UtcNow });
        Db.Users.Add(new User { Id = userId, Email = "del@t.com", FirstName = "D", LastName = "T", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        Db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId, Role = UserRole.Student, Status = "active" });
        await Db.SaveChangesAsync();

        var tenant = await Db.Tenants.FindAsync(tenantId);
        Db.Tenants.Remove(tenant!);
        await Db.SaveChangesAsync();

        var memberships = await Db.TenantMemberships.Where(m => m.TenantId == tenantId).CountAsync();
        memberships.Should().Be(0);
    }
}
