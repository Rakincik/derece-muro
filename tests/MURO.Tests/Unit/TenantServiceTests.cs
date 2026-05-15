using FluentAssertions;
using MURO.Application.DTOs.Tenants;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// TenantService için unit testler — Tenant context yönetimi,
/// feature flag kontrolü, connection string erişimi.
/// </summary>
public class TenantServiceTests
{
    private readonly TenantService _service = new(null!);

    private TenantInfo MakeTenant(Dictionary<string, bool>? features = null) =>
        new(Guid.NewGuid(), "Test Okul", "testokul", "/logo.png", "/favicon.ico",
            "#3b82f6", "#8b5cf6", "© Test", "https://bbb.test.com", "secret",
            "Host=localhost;Database=test", true,
            features ?? new Dictionary<string, bool> { { "podcast", true }, { "exam", false } });

    // ────────────────────────────────────────────────────────────────────────
    // SET CURRENT TENANT (ID only)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetCurrentTenant_ById_ShouldSetId()
    {
        var id = Guid.NewGuid();
        _service.SetCurrentTenant(id);

        _service.CurrentTenantId.Should().Be(id);
        _service.CurrentTenant.Should().BeNull();
    }

    // ────────────────────────────────────────────────────────────────────────
    // SET CURRENT TENANT (Full Info)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetCurrentTenant_ByInfo_ShouldSetBothIdAndInfo()
    {
        var info = MakeTenant();
        _service.SetCurrentTenant(info);

        _service.CurrentTenantId.Should().Be(info.Id);
        _service.CurrentTenant.Should().Be(info);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET CONNECTION STRING
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetConnectionString_AfterSet_ShouldReturn()
    {
        _service.SetCurrentTenant(MakeTenant());
        _service.GetConnectionString().Should().Be("Host=localhost;Database=test");
    }

    [Fact]
    public void GetConnectionString_BeforeSet_ShouldReturnNull()
    {
        _service.GetConnectionString().Should().BeNull();
    }

    // ────────────────────────────────────────────────────────────────────────
    // HAS FEATURE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HasFeature_EnabledFeature_ShouldReturnTrue()
    {
        _service.SetCurrentTenant(MakeTenant());
        _service.HasFeature("podcast").Should().BeTrue();
    }

    [Fact]
    public void HasFeature_DisabledFeature_ShouldReturnFalse()
    {
        _service.SetCurrentTenant(MakeTenant());
        _service.HasFeature("exam").Should().BeFalse();
    }

    [Fact]
    public void HasFeature_NonExistentFeature_ShouldReturnFalse()
    {
        _service.SetCurrentTenant(MakeTenant());
        _service.HasFeature("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void HasFeature_NoTenantSet_ShouldReturnFalse()
    {
        _service.HasFeature("podcast").Should().BeFalse();
    }

    [Fact]
    public void HasFeature_NullFeatures_ShouldReturnFalse()
    {
        _service.SetCurrentTenant(new TenantInfo(
            Guid.NewGuid(), "Test", null, null, null, null, null, null,
            null, null, null, true, null!));
        _service.HasFeature("podcast").Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // OVERWRITE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetCurrentTenant_Twice_ShouldOverwrite()
    {
        var info1 = MakeTenant();
        var info2 = MakeTenant(new Dictionary<string, bool> { { "live", true } });
        _service.SetCurrentTenant(info1);
        _service.SetCurrentTenant(info2);

        _service.CurrentTenantId.Should().Be(info2.Id);
        _service.HasFeature("live").Should().BeTrue();
        _service.HasFeature("podcast").Should().BeFalse(); // info1'den taşınmamalı
    }
}
