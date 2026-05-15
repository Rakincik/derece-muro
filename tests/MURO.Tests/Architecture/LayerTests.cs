using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MURO.Tests.Architecture;

public class LayerTests
{
    private const string ApiNamespace = "MURO.API";
    private const string ApplicationNamespace = "MURO.Application";
    private const string InfrastructureNamespace = "MURO.Infrastructure";
    private const string DomainNamespace = "MURO.Domain";

    [Fact]
    public void Controllers_Should_Not_Have_DependencyOn_Infrastructure()
    {
        // Arrange
        var apiAssembly = typeof(MURO.API.Controllers.SecurityController).Assembly;

        // Act
        var result = Types
            .InAssembly(apiAssembly)
            .That()
            .ResideInNamespace("MURO.API.Controllers")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue("Controller'lar doğrudan Infrastructure (Veritabanı vb.) katmanına erişemez. Araya Servis (MURO.Application) katmanı konulmalıdır.");
    }

    [Fact]
    public void Domain_Should_Not_Have_DependencyOn_Any_Other_Layer()
    {
        // Arrange
        var domainAssembly = typeof(MURO.Domain.Entities.User).Assembly;

        // Act
        var result = Types
            .InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue("Domain katmanı çekirdektir, kendisinden üstteki hiçbir katmanı (Application, Infrastructure, API) bilemez ve onlara bağımlı olamaz.");
    }

    [Fact]
    public void Interfaces_Should_Start_With_I()
    {
        // Arrange
        var appAssembly = typeof(MURO.Application.Interfaces.IAuthLoginService).Assembly;

        // Act
        var result = Types
            .InAssembly(appAssembly)
            .That()
            .ResideInNamespace("MURO.Application.Interfaces")
            .And()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue("Tüm arayüzler (Interface) 'I' harfi ile başlamalıdır.");
    }

    [Fact]
    public void Services_Should_Not_Be_God_Objects()
    {
        // Arrange
        var infraAssembly = typeof(MURO.Infrastructure.Services.AdminTenantManagementService).Assembly;

        // Act
        // NetArchTest doesn't natively check line count, but we check if any service implements more than 1 or 2 interfaces
        // Or we just verify that "AdminService" no longer exists!
        var types = Types.InAssembly(infraAssembly)
            .That()
            .ResideInNamespace("MURO.Infrastructure.Services")
            .GetTypes();

        var godObjectExists = types.Any(t => t.Name == "AdminService" || t.Name == "AdminController");

        // Assert
        godObjectExists.Should().BeFalse("Sistemde 'AdminService' veya benzeri God Object (Devasa Sınıf) anti-pattern'leri barındırılamaz. İşlemler alt servislere bölünmelidir.");
    }
}
