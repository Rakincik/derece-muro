using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MURO.Application.DTOs.Calendar;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Takvim E2E — Etkinlik CRUD, tarih filtreleri (from/to, year/month),
/// çoklu etkinlik, sıralama, çapraz rol erişimi, edge case'ler.
/// </summary>
[Trait("Category", "E2E")]
public class CalendarE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public CalendarE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ════════════════════════════════════════════════════════════════════════
    // 1. TAM YAŞAM DÖNGÜSÜ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CalendarLifecycle_CreateGetUpdateDelete_FullFlow()
    {
        var admin = _f.CreateAdminClient();

        // Create
        var createRes = await admin.PostAsJsonAsync("/api/v1/calendar",
            new CreateCalendarEventRequest("Vize Haftası", "Tüm dersler vize",
                "Sınav", DateTime.UtcNow.AddDays(10),
                DateTime.UtcNow.AddDays(10).AddHours(3), null, null, "#e74c3c"));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var ev = await createRes.Content.ReadFromJsonAsync<CalendarEventDto>();
        ev!.Title.Should().Be("Vize Haftası");
        ev.Color.Should().Be("#e74c3c");

        // Get by ID
        var getRes = await admin.GetAsync($"/api/v1/calendar/{ev.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getRes.Content.ReadFromJsonAsync<CalendarEventDto>();
        detail!.Description.Should().Be("Tüm dersler vize");

        // Update — tüm alanlar değişmeli
        var updateRes = await admin.PutAsJsonAsync($"/api/v1/calendar/{ev.Id}",
            new UpdateCalendarEventRequest("Final Haftası", "Final sınavları",
                "Sınav", DateTime.UtcNow.AddDays(30),
                DateTime.UtcNow.AddDays(30).AddHours(4), null, null, "#2ecc71"));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<CalendarEventDto>();
        updated!.Title.Should().Be("Final Haftası");
        updated.Color.Should().Be("#2ecc71");

        // Delete
        var delRes = await admin.DeleteAsync($"/api/v1/calendar/{ev.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 404
        var afterDel = await admin.GetAsync($"/api/v1/calendar/{ev.Id}");
        afterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. TARİH FİLTRESİ — year/month parametreleri
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CalendarList_YearMonthFilter_ShouldReturnCorrectMonth()
    {
        var admin = _f.CreateAdminClient();
        var now = DateTime.UtcNow;

        // Bu ay için etkinlik
        await admin.PostAsJsonAsync("/api/v1/calendar",
            new CreateCalendarEventRequest("Bu Ayın Etkinliği", null, "Ders",
                now.AddDays(1), now.AddDays(1).AddHours(1), null, null, "#333"));

        // Bu ay ile filtrele
        var res = await admin.GetAsync($"/api/v1/calendar?year={now.Year}&month={now.Month}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await res.Content.ReadFromJsonAsync<List<CalendarEventDto>>();
        events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CalendarList_FromToFilter_ShouldWork()
    {
        var admin = _f.CreateAdminClient();

        // 20 gün sonrası için etkinlik
        var futureDate = DateTime.UtcNow.AddDays(20);
        await admin.PostAsJsonAsync("/api/v1/calendar",
            new CreateCalendarEventRequest("Gelecek Etkinlik", null, "Toplantı",
                futureDate, futureDate.AddHours(2), null, null, "#444"));

        // from/to ile dar aralık
        var from = futureDate.AddDays(-1).ToString("o");
        var to = futureDate.AddDays(1).ToString("o");
        var res = await admin.GetAsync($"/api/v1/calendar?from={from}&to={to}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await res.Content.ReadFromJsonAsync<List<CalendarEventDto>>();
        events.Should().Contain(e => e.Title == "Gelecek Etkinlik");
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. ÇOKLU ETKİNLİK OLUŞTURMA + LİSTE TUTARLILIĞI
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CalendarMultipleEvents_ListShouldContainAll()
    {
        var admin = _f.CreateAdminClient();
        var titles = new[] { "Etkinlik A", "Etkinlik B", "Etkinlik C" };

        foreach (var t in titles)
        {
            await admin.PostAsJsonAsync("/api/v1/calendar",
                new CreateCalendarEventRequest(t, null, "Diğer",
                    DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(1),
                    null, null, "#555"));
        }

        var listRes = await admin.GetAsync(
            $"/api/v1/calendar?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}");
        var events = await listRes.Content.ReadFromJsonAsync<List<CalendarEventDto>>();
        foreach (var t in titles)
            events.Should().Contain(e => e.Title == t);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. ÖĞRENCİ ERİŞİMİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Calendar_StudentCanAccessList()
    {
        var res = await _f.CreateStudentClient().GetAsync("/api/v1/calendar");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Calendar_StudentCanViewEventDetail()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();

        var r = await admin.PostAsJsonAsync("/api/v1/calendar",
            new CreateCalendarEventRequest("Student Detay Test", null, "Ders",
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(3).AddHours(1),
                null, null, "#666"));
        var ev = await r.Content.ReadFromJsonAsync<CalendarEventDto>();

        var res = await student.GetAsync($"/api/v1/calendar/{ev!.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. AUTH GUARD
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Calendar_NoAuth_ShouldReturn401()
    {
        (await _f.CreateAnonymousClient().GetAsync("/api/v1/calendar"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. EDGE CASES
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetEvent_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().GetAsync($"/api/v1/calendar/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEvent_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().DeleteAsync($"/api/v1/calendar/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEvent_Twice_SecondShouldReturn404()
    {
        var admin = _f.CreateAdminClient();
        var r = await admin.PostAsJsonAsync("/api/v1/calendar",
            new CreateCalendarEventRequest("Çift Sil", null, "X",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1),
                null, null, "#000"));
        var ev = await r.Content.ReadFromJsonAsync<CalendarEventDto>();

        (await admin.DeleteAsync($"/api/v1/calendar/{ev!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/v1/calendar/{ev.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
