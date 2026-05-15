using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Support;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Destek sistemi E2E — Ticket yaşam döngüsü, çoklu mesaj zinciri,
/// status filtresi, FAQ CRUD, sıralama, yetki kontrolleri.
/// </summary>
[Trait("Category", "E2E")]
public class SupportE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public SupportE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ════════════════════════════════════════════════════════════════════════
    // 1. TAM TICKET YAŞAM DÖNGÜSÜ (Öğrenci + Admin perspektifi)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TicketLifecycle_CreateReplyClose_FullFlow()
    {
        var student = _f.CreateStudentClient();
        var admin = _f.CreateAdminClient();

        // 1. Öğrenci ticket oluşturur
        var createRes = await student.PostAsJsonAsync("/api/v1/support/tickets",
            new CreateTicketRequest("Video açılmıyor",
                "Fizik 101 dersinin 3. videosunu açamıyorum, hata veriyor.",
                "High", "Technical"));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketListDto>();
        ticket!.Subject.Should().Be("Video açılmıyor");
        ticket.Status.Should().Be("Open");

        // 2. Ticket detayı — mesajlar dahil
        var detailRes = await admin.GetAsync($"/api/v1/support/tickets/{ticket.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<TicketDetailDto>();
        detail!.Messages.Should().NotBeEmpty(); // İlk mesaj otomatik oluşturulmalı

        // 3. Admin yanıt verir (ilk yanıt)
        var reply1 = await admin.PostAsJsonAsync(
            $"/api/v1/support/tickets/{ticket.Id}/reply",
            new ReplyTicketRequest("Hangi tarayıcıyı kullanıyorsunuz?"));
        reply1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Öğrenci tekrar yanıt verir (çoklu mesaj zinciri)
        var reply2 = await student.PostAsJsonAsync(
            $"/api/v1/support/tickets/{ticket.Id}/reply",
            new ReplyTicketRequest("Chrome 120, Windows 11 kullanıyorum."));
        reply2.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Admin çözüm yanıtı
        var reply3 = await admin.PostAsJsonAsync(
            $"/api/v1/support/tickets/{ticket.Id}/reply",
            new ReplyTicketRequest("Cache temizleyin ve tekrar deneyin."));
        reply3.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Detayda tüm mesajlar görünmeli (en az 4: ilk + 3 reply)
        var finalDetail = await admin.GetAsync($"/api/v1/support/tickets/{ticket.Id}");
        var fd = await finalDetail.Content.ReadFromJsonAsync<TicketDetailDto>();
        fd!.Messages.Should().HaveCountGreaterThanOrEqualTo(4);

        // 7. Admin ticket'ı kapatır
        var closeRes = await admin.PutAsync(
            $"/api/v1/support/tickets/{ticket.Id}/close", null);
        closeRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. TICKET STATUS FİLTRESİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TicketList_StatusFilter_ShouldWork()
    {
        var student = _f.CreateStudentClient();
        var admin = _f.CreateAdminClient();

        // Açık ticket oluştur
        await student.PostAsJsonAsync("/api/v1/support/tickets",
            new CreateTicketRequest("Filtre Test", "x", "Medium", "General"));

        // Open filtrelemesi
        var openRes = await admin.GetAsync("/api/v1/support/tickets?status=Open");
        openRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var openList = await openRes.Content.ReadFromJsonAsync<PagedResult<TicketListDto>>();
        openList!.Items.Should().AllSatisfy(t => t.Status.Should().Be("Open"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. TICKET SAYFALAMA
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TicketList_Pagination_ShouldWork()
    {
        var student = _f.CreateStudentClient();
        var admin = _f.CreateAdminClient();

        for (int i = 0; i < 5; i++)
            await student.PostAsJsonAsync("/api/v1/support/tickets",
                new CreateTicketRequest($"Page Ticket {i}", "x", "Medium", "General"));

        var p1 = await admin.GetAsync("/api/v1/support/tickets?page=1&pageSize=2");
        var page1 = await p1.Content.ReadFromJsonAsync<PagedResult<TicketListDto>>();
        page1!.Items.Should().HaveCount(2);
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(5);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. FAQ YAŞAM DÖNGÜSÜ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FaqLifecycle_CreateUpdateDeleteList_ShouldWork()
    {
        var admin = _f.CreateAdminClient();

        // Create FAQ 1
        var faq1Res = await admin.PostAsJsonAsync("/api/v1/support/faq",
            new CreateFaqRequest("Nasıl kayıt olunur?",
                "Ana sayfadan kayıt butonuna tıklayın.", "Genel", 1));
        faq1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var faq1 = await faq1Res.Content.ReadFromJsonAsync<FaqDto>();

        // Create FAQ 2
        var faq2Res = await admin.PostAsJsonAsync("/api/v1/support/faq",
            new CreateFaqRequest("Şifremi unuttum, ne yapmalıyım?",
                "Giriş sayfasında 'Şifremi Unuttum' bağlantısına tıklayın.", "Hesap", 2));
        faq2Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // List — ikisi de olmalı
        var listRes = await admin.GetAsync("/api/v1/support/faq");
        var faqs = await listRes.Content.ReadFromJsonAsync<List<FaqDto>>();
        faqs.Should().HaveCountGreaterThanOrEqualTo(2);

        // Update FAQ 1
        var updateRes = await admin.PutAsJsonAsync($"/api/v1/support/faq/{faq1!.Id}",
            new UpdateFaqRequest("Nasıl kayıt olunur? (Güncel)",
                "Web sitesinden veya mobil uygulamadan.", "Genel", 1));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<FaqDto>();
        updated!.AnswerText.Should().Contain("mobil");

        // Delete FAQ 1
        var delRes = await admin.DeleteAsync($"/api/v1/support/faq/{faq1.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Silinen FAQ artık listede olmamalı
        var afterDel = await admin.GetAsync("/api/v1/support/faq");
        var remaining = await afterDel.Content.ReadFromJsonAsync<List<FaqDto>>();
        remaining.Should().NotContain(f => f.Id == faq1.Id);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. AUTH GUARD
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Support_NoAuth_ShouldReturn401()
    {
        var anon = _f.CreateAnonymousClient();
        (await anon.GetAsync("/api/v1/support/tickets"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync("/api/v1/support/faq"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. EDGE: Var olmayan ticket
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTicket_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient()
            .GetAsync($"/api/v1/support/tickets/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
