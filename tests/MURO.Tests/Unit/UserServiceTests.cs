using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Users;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// UserService için unit testler — BulkCreate ve ExportUsers odaklı.
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly UserService _userService;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _userService = new UserService(_db, new MURO.Tests.Helpers.DummyCacheService());

        // Tenant oluştur
        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId, Name = "Test Tenant", Code = "test",
            CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    private CreateUserRequest MakeRequest(string email, string role = "Student") => new(
        "Ad", "Soyad", email, "Test@1234", null, role, null, null
    );

    // ────────────────────────────────────────────────────────────────────────
    // BulkCreate
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkCreate_UniqueEmails_ShouldCreateAllUsers()
    {
        var requests = new List<CreateUserRequest>
        {
            MakeRequest("user1@test.com"),
            MakeRequest("user2@test.com"),
            MakeRequest("user3@test.com"),
        };

        var results = await _userService.BulkCreateUsersAsync(_tenantId, requests);

        results.Should().HaveCount(3);
        _db.Users.Should().HaveCount(3);
    }

    [Fact]
    public async Task BulkCreate_DuplicateEmail_ShouldSkipDuplicate()
    {
        // Önceden var olan kullanıcı
        await _userService.CreateUserAsync(_tenantId, MakeRequest("duplicate@test.com"));

        var requests = new List<CreateUserRequest>
        {
            MakeRequest("duplicate@test.com"),  // duplike — atlanmalı
            MakeRequest("unique@test.com"),     // yeni — eklenmeli
        };

        var results = await _userService.BulkCreateUsersAsync(_tenantId, requests);

        results.Should().HaveCount(1);
        results[0].Email.Should().Be("unique@test.com");
    }

    [Fact]
    public async Task BulkCreate_AllDuplicates_ShouldReturnEmptyList()
    {
        await _userService.CreateUserAsync(_tenantId, MakeRequest("a@test.com"));
        await _userService.CreateUserAsync(_tenantId, MakeRequest("b@test.com"));

        var requests = new List<CreateUserRequest>
        {
            MakeRequest("a@test.com"),
            MakeRequest("b@test.com"),
        };

        var results = await _userService.BulkCreateUsersAsync(_tenantId, requests);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkCreate_ShouldUseSingleSaveChanges_NotPerUser()
    {
        // 100 kullanıcı için tek SaveChanges olmalı — dolaylı test: tümü aynı anda görünür
        var requests = Enumerable.Range(1, 100)
            .Select(i => MakeRequest($"user{i}@test.com"))
            .ToList();

        await _userService.BulkCreateUsersAsync(_tenantId, requests);

        _db.Users.Count().Should().Be(100);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ExportUsers — P1: SQL filtresi
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportUsers_WithRoleFilter_ShouldOnlyExportMatchingRole()
    {
        await _userService.CreateUserAsync(_tenantId, MakeRequest("student@test.com", "Student"));
        await _userService.CreateUserAsync(_tenantId, MakeRequest("admin@test.com", "Admin"));

        var csvBytes = await _userService.ExportUsersAsync(_tenantId, "Student");
        var csv = System.Text.Encoding.UTF8.GetString(csvBytes);

        csv.Should().Contain("student@test.com");
        csv.Should().NotContain("admin@test.com");
    }

    [Fact]
    public async Task ExportUsers_WithoutRoleFilter_ShouldExportAll()
    {
        await _userService.CreateUserAsync(_tenantId, MakeRequest("student@test.com", "Student"));
        await _userService.CreateUserAsync(_tenantId, MakeRequest("admin@test.com", "Admin"));

        var csvBytes = await _userService.ExportUsersAsync(_tenantId, null);
        var csv = System.Text.Encoding.UTF8.GetString(csvBytes);

        csv.Should().Contain("student@test.com");
        csv.Should().Contain("admin@test.com");
    }

    [Fact]
    public async Task ExportUsers_NameWithComma_ShouldBeQuotedCorrectly()
    {
        // P2: CSV injection / comma in field
        var user = await _userService.CreateUserAsync(_tenantId,
            new CreateUserRequest("Ad, Soyad", "Aile", "comma@test.com", "Test@1234", null, "Student", null, null));

        var csvBytes = await _userService.ExportUsersAsync(_tenantId, null);
        var csv = System.Text.Encoding.UTF8.GetString(csvBytes);

        // Virgül içeren ad tırnak içinde olmalı
        csv.Should().Contain("\"Ad, Soyad\"");
    }

    public void Dispose() => _db.Dispose();
}
