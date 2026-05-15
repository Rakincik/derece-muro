using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Groups;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

public class GroupServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly GroupService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _user2 = Guid.NewGuid();
    private readonly Guid _user3 = Guid.NewGuid();

    public GroupServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new MuroDbContext(options);
        _service = new GroupService(_db, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "T", Code = "grp", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _user1, Email = "u1@t.com", FirstName = "Ali", LastName = "U", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Users.Add(new User { Id = _user2, Email = "u2@t.com", FirstName = "Ayşe", LastName = "U", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Users.Add(new User { Id = _user3, Email = "u3@t.com", FirstName = "Veli", LastName = "U", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.SaveChanges();
    }

    private CreateGroupRequest Grp(string name, Guid? parent = null, string? color = null)
        => new(name, null, parent, color, null);

    [Fact]
    public async Task CreateGroup_ShouldSaveToDb()
    {
        var r = await _service.CreateGroupAsync(_tenantId, new CreateGroupRequest("Sınıf A", "Fizik sınıfı", null, "#4ade80", null));
        r.Name.Should().Be("Sınıf A");
        (await _db.Groups.FirstAsync(g => g.Name == "Sınıf A")).TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task CreateSubGroup_ShouldSetParent()
    {
        var parent = await _service.CreateGroupAsync(_tenantId, Grp("Ana Grup"));
        var child = await _service.CreateGroupAsync(_tenantId, Grp("Alt Grup", parent.Id));
        child.ParentGroupId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task CreateSubGroup_InvalidParent_ShouldThrow()
    {
        var act = () => _service.CreateGroupAsync(_tenantId, Grp("Yetim", Guid.NewGuid()));
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Üst grup*");
    }

    [Fact]
    public async Task UpdateGroup_ShouldModifyFields()
    {
        var c = await _service.CreateGroupAsync(_tenantId, Grp("Eski"));
        var u = await _service.UpdateGroupAsync(_tenantId, c.Id, new UpdateGroupRequest("Yeni İsim", null, null, "#ff0000", null));
        u.Name.Should().Be("Yeni İsim");
        u.Color.Should().Be("#ff0000");
    }

    [Fact]
    public async Task UpdateGroup_SelfParent_ShouldThrow()
    {
        var g = await _service.CreateGroupAsync(_tenantId, Grp("Loop"));
        var act = () => _service.UpdateGroupAsync(_tenantId, g.Id, new UpdateGroupRequest(null, null, g.Id, null, null));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*kendi kendisinin*");
    }

    [Fact]
    public async Task DeleteGroup_ShouldRemove()
    {
        var g = await _service.CreateGroupAsync(_tenantId, Grp("Silinecek"));
        await _service.DeleteGroupAsync(_tenantId, g.Id);
        (await _db.Groups.AnyAsync(x => x.Id == g.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteGroup_WithChildren_ShouldThrow()
    {
        var p = await _service.CreateGroupAsync(_tenantId, Grp("Parent"));
        await _service.CreateGroupAsync(_tenantId, Grp("Child", p.Id));
        var act = () => _service.DeleteGroupAsync(_tenantId, p.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Alt grupları*");
    }

    [Fact]
    public async Task AddMembers_ShouldAddToGroup()
    {
        var g = await _service.CreateGroupAsync(_tenantId, Grp("Üyeli"));
        await _service.AddMembersAsync(_tenantId, g.Id, new List<Guid> { _user1, _user2 });
        var m = await _db.GroupMembers.Where(gm => gm.GroupId == g.Id).ToListAsync();
        m.Should().HaveCount(2);
        m.All(x => x.Status == "active").Should().BeTrue();
    }

    [Fact]
    public async Task AddMembers_Duplicate_ShouldNotAddAgain()
    {
        var g = await _service.CreateGroupAsync(_tenantId, Grp("NoD"));
        await _service.AddMembersAsync(_tenantId, g.Id, new List<Guid> { _user1 });
        await _service.AddMembersAsync(_tenantId, g.Id, new List<Guid> { _user1, _user2 });
        (await _db.GroupMembers.CountAsync(gm => gm.GroupId == g.Id)).Should().Be(2);
    }

    [Fact]
    public async Task RemoveMember_ShouldSetStatusRemoved()
    {
        var g = await _service.CreateGroupAsync(_tenantId, Grp("RM"));
        await _service.AddMembersAsync(_tenantId, g.Id, new List<Guid> { _user1 });
        await _service.RemoveMemberAsync(_tenantId, g.Id, _user1);
        (await _db.GroupMembers.FirstAsync(gm => gm.GroupId == g.Id && gm.UserId == _user1)).Status.Should().Be("removed");
    }

    [Fact]
    public async Task MoveMembers_ShouldTransfer()
    {
        var a = await _service.CreateGroupAsync(_tenantId, Grp("A"));
        var b = await _service.CreateGroupAsync(_tenantId, Grp("B"));
        await _service.AddMembersAsync(_tenantId, a.Id, new List<Guid> { _user1, _user2 });
        await _service.MoveMembersAsync(_tenantId, a.Id, b.Id, new List<Guid> { _user1 });
        (await _db.GroupMembers.CountAsync(gm => gm.GroupId == a.Id && gm.Status == "active")).Should().Be(1);
        (await _db.GroupMembers.CountAsync(gm => gm.GroupId == b.Id && gm.Status == "active")).Should().Be(1);
    }

    [Fact]
    public async Task GetGroupTree_ShouldReturnHierarchy()
    {
        var root = await _service.CreateGroupAsync(_tenantId, Grp("Okul"));
        await _service.CreateGroupAsync(_tenantId, Grp("Sınıf A", root.Id));
        await _service.CreateGroupAsync(_tenantId, Grp("Sınıf B", root.Id));
        var tree = await _service.GetGroupTreeAsync(_tenantId);
        tree.Should().HaveCount(1);
        tree[0].Children.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetGroups_Search_ShouldFilter()
    {
        await _service.CreateGroupAsync(_tenantId, Grp("Fizik Sınıfı"));
        await _service.CreateGroupAsync(_tenantId, Grp("Matematik Sınıfı"));
        await _service.CreateGroupAsync(_tenantId, Grp("Biyoloji"));
        (await _service.GetGroupsAsync(_tenantId, 1, 10, "Sınıf")).Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetGroups_Paging_ShouldWork()
    {
        for (int i = 0; i < 25; i++) await _service.CreateGroupAsync(_tenantId, Grp($"G{i:D2}"));
        var p1 = await _service.GetGroupsAsync(_tenantId, 1, 10, null);
        var p3 = await _service.GetGroupsAsync(_tenantId, 3, 10, null);
        p1.Items.Should().HaveCount(10);
        p3.Items.Should().HaveCount(5);
        p1.TotalCount.Should().Be(25);
    }

    public void Dispose() => _db.Dispose();
}
