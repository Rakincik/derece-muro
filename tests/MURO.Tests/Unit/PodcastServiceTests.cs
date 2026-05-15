using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MURO.Application.DTOs.Podcasts;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using System.Net;
using System.Text;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// PodcastService için unit testler — CRUD, AI üretimi, durum geçişi,
/// sayfalama, kurs filtresi, silme.
/// </summary>
public class PodcastServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly PodcastService _service;
    private readonly Mock<IGeminiService> _geminiMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();

    public PodcastServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _geminiMock = new Mock<IGeminiService>();

        // EdgeTtsClient → gerçek instance ama HTTP handler mock'lanıyor
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xFB, 0x90 }) // MP3 header
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var edgeTtsConfig = new Mock<IConfiguration>();
        edgeTtsConfig.Setup(c => c["EdgeTts:BaseUrl"]).Returns("http://localhost:5050");
        var edgeTtsLogger = new Mock<ILogger<EdgeTtsClient>>();
        var edgeTts = new EdgeTtsClient(httpClient, edgeTtsConfig.Object, edgeTtsLogger.Object);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Podcast:WwwrootPath"]).Returns(Path.GetTempPath());

        _service = new PodcastService(_db, _geminiMock.Object, edgeTts, configMock.Object, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "pod" });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101" });
        _db.SaveChanges();
    }

    // ════════════════════════════════════════════════════════════════════════
    // CREATE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Create_ShouldSaveAsProcessing()
    {
        var result = await _service.CreateAsync(_tenantId,
            new CreatePodcastRequest(_courseId, "Podcast 1", "İçerik metni", null));

        result.Title.Should().Be("Podcast 1");
        result.Status.Should().Be(MediaStatus.Processing);
        result.CourseTitle.Should().Be("Fizik 101");
        (await _db.Podcasts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_WithoutCourse_ShouldHaveNullCourseTitle()
    {
        var result = await _service.CreateAsync(_tenantId,
            new CreatePodcastRequest(null, "Genel Podcast", null, null));
        result.CourseTitle.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET BY ID
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetById_ShouldReturn()
    {
        var created = await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "P1", null, null));
        var result = await _service.GetByIdAsync(_tenantId, created.Id);
        result.Title.Should().Be("P1");
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Podcast bulunamadı*");
    }

    [Fact]
    public async Task GetById_WrongTenant_ShouldThrow()
    {
        var created = await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "P1", null, null));
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), created.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET BY ID FOR STREAM (anonymous)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdForStream_ShouldReturnWithoutTenantCheck()
    {
        var created = await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "Stream Me", null, null));
        var result = await _service.GetByIdForStreamAsync(created.Id);
        result.Should().NotBeNull();
        result!.Title.Should().Be("Stream Me");
    }

    [Fact]
    public async Task GetByIdForStream_NonExistent_ShouldReturnNull()
    {
        var result = await _service.GetByIdForStreamAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════
    // DURUM GEÇİŞİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_ShouldChangeStatus()
    {
        var created = await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "P1", null, null));
        var updated = await _service.UpdateStatusAsync(_tenantId, created.Id, "Ready");
        updated.Status.Should().Be(MediaStatus.Ready);
    }

    [Fact]
    public async Task UpdateStatus_InvalidEnumValue_ShouldNotChange()
    {
        var created = await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "P1", null, null));
        var updated = await _service.UpdateStatusAsync(_tenantId, created.Id, "GeçersizDurum");
        updated.Status.Should().Be(MediaStatus.Processing);
    }

    [Fact]
    public async Task UpdateStatus_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateStatusAsync(_tenantId, Guid.NewGuid(), "Ready");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // DELETE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Delete_ShouldRemoveFromDb()
    {
        var created = await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "Sil", null, null));
        await _service.DeleteAsync(_tenantId, created.Id);
        (await _db.Podcasts.AnyAsync(p => p.Id == created.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonExistent_ShouldThrow()
    {
        var act = () => _service.DeleteAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // LIST (Paging, CourseFilter)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPodcasts_Paging_ShouldWork()
    {
        for (int i = 0; i < 15; i++)
            await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, $"P{i}", null, null));

        var p1 = await _service.GetPodcastsAsync(_tenantId, 1, 10, null);
        var p2 = await _service.GetPodcastsAsync(_tenantId, 2, 10, null);

        p1.Items.Should().HaveCount(10);
        p2.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetPodcasts_CourseFilter_ShouldFilter()
    {
        await _service.CreateAsync(_tenantId, new CreatePodcastRequest(_courseId, "Kurs Podcast", null, null));
        await _service.CreateAsync(_tenantId, new CreatePodcastRequest(null, "Genel Podcast", null, null));

        var result = await _service.GetPodcastsAsync(_tenantId, 1, 10, _courseId);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Kurs Podcast");
    }

    // ════════════════════════════════════════════════════════════════════════
    // AI GENERATE — HttpHandler mock ile gerçek EdgeTtsClient
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_Success_ShouldSetReadyStatus()
    {
        _geminiMock.Setup(g => g.EnhanceToScriptAsync(It.IsAny<string>()))
            .ReturnsAsync("Profesyonel podcast script içeriği...");

        var result = await _service.GenerateAsync(_tenantId,
            new GeneratePodcastRequest("AI Podcast", null, "Ham metin içerik"));

        result.Status.Should().Be(MediaStatus.Ready);
        result.AudioFilePath.Should().StartWith("/podcasts/");
        result.GeneratedScript.Should().Be("Profesyonel podcast script içeriği...");
    }

    [Fact]
    public async Task Generate_GeminiFails_ShouldSetFailedAndThrow()
    {
        _geminiMock.Setup(g => g.EnhanceToScriptAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Gemini API hatası"));

        var act = () => _service.GenerateAsync(_tenantId,
            new GeneratePodcastRequest("Fail Podcast", null, "text"));

        await act.Should().ThrowAsync<Exception>();

        var podcast = await _db.Podcasts.FirstAsync();
        podcast.Status.Should().Be(MediaStatus.Failed);
    }

    public void Dispose() => _db.Dispose();
}
