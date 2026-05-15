using ClosedXML.Excel;
using FluentAssertions;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// ExcelService için unit testler — Export, Import, Template oluşturma,
/// boş veri, reflection-based property mapping, round-trip doğrulaması.
/// </summary>
public class ExcelServiceTests
{
    private readonly ExcelService _service = new();

    // Test DTO
    public class SampleDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Email { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════════
    // EXPORT TO EXCEL
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExportToExcel_ShouldReturnValidXlsx()
    {
        var data = new[] { new SampleDto { Name = "Ali", Age = 25, Email = "a@t.com" } };

        var bytes = _service.ExportToExcel(data, "Users");

        bytes.Should().NotBeEmpty();
        // .xlsx magic bytes (PK — zip header)
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_ShouldContainHeaders()
    {
        var data = new[] { new SampleDto { Name = "Ali", Age = 25 } };
        var bytes = _service.ExportToExcel(data, "Sheet1");

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(1, 2).GetString().Should().Be("Age");
        ws.Cell(1, 3).GetString().Should().Be("Email");
    }

    [Fact]
    public void ExportToExcel_ShouldContainData()
    {
        var data = new[]
        {
            new SampleDto { Name = "Ali", Age = 25, Email = "ali@test.com" },
            new SampleDto { Name = "Veli", Age = 30, Email = "veli@test.com" },
        };
        var bytes = _service.ExportToExcel(data, "Sheet1");

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        ws.Cell(2, 1).GetString().Should().Be("Ali");
        ws.Cell(2, 2).GetString().Should().Be("25");
        ws.Cell(3, 1).GetString().Should().Be("Veli");
    }

    [Fact]
    public void ExportToExcel_EmptyList_ShouldReturnHeaderOnly()
    {
        var data = Array.Empty<SampleDto>();
        var bytes = _service.ExportToExcel(data, "Empty");

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(2, 1).GetString().Should().BeEmpty();
    }

    [Fact]
    public void ExportToExcel_NullableField_ShouldExportEmpty()
    {
        var data = new[] { new SampleDto { Name = "Ali", Age = 25, Email = null } };
        var bytes = _service.ExportToExcel(data);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        ws.Cell(2, 3).GetString().Should().BeEmpty();
    }

    [Fact]
    public void ExportToExcel_CustomSheetName_ShouldSetName()
    {
        var bytes = _service.ExportToExcel(new[] { new SampleDto { Name = "X" } }, "Öğrenciler");

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        wb.Worksheet(1).Name.Should().Be("Öğrenciler");
    }

    // ════════════════════════════════════════════════════════════════════════
    // IMPORT FROM EXCEL
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ImportFromExcel_ShouldParseCorrectly()
    {
        // Önce export et, sonra import et (round-trip)
        var original = new[]
        {
            new SampleDto { Name = "Ali", Age = 25, Email = "ali@t.com" },
            new SampleDto { Name = "Veli", Age = 30, Email = "veli@t.com" },
        };
        var bytes = _service.ExportToExcel(original);

        using var stream = new MemoryStream(bytes);
        var imported = _service.ImportFromExcel<SampleDto>(stream);

        imported.Should().HaveCount(2);
        imported[0].Name.Should().Be("Ali");
        imported[0].Age.Should().Be(25);
        imported[1].Name.Should().Be("Veli");
    }

    [Fact]
    public void ImportFromExcel_EmptySheet_ShouldReturnEmptyList()
    {
        // Boş Excel oluştur
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Empty");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Age";
        ws.Cell(1, 3).Value = "Email";

        using var mem = new MemoryStream();
        wb.SaveAs(mem);
        mem.Position = 0;

        var imported = _service.ImportFromExcel<SampleDto>(mem);
        imported.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GENERATE TEMPLATE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateTemplate_ShouldHaveHeadersOnly()
    {
        var bytes = _service.GenerateTemplate<SampleDto>("Şablon");

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        ws.Name.Should().Be("Şablon");
        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(1, 2).GetString().Should().Be("Age");
        ws.Cell(2, 1).GetString().Should().BeEmpty();
    }

    [Fact]
    public void GenerateTemplate_HeadersShouldBeBold()
    {
        var bytes = _service.GenerateTemplate<SampleDto>();

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        ws.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
    }

    // ════════════════════════════════════════════════════════════════════════
    // ROUND-TRIP BÜYÜK VERİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RoundTrip_100Rows_ShouldPreserveAll()
    {
        var original = Enumerable.Range(1, 100)
            .Select(i => new SampleDto { Name = $"User{i}", Age = 20 + (i % 50), Email = $"u{i}@t.com" })
            .ToArray();

        var bytes = _service.ExportToExcel(original);
        using var stream = new MemoryStream(bytes);
        var imported = _service.ImportFromExcel<SampleDto>(stream);

        imported.Should().HaveCount(100);
        imported[0].Name.Should().Be("User1");
        imported[99].Name.Should().Be("User100");
    }
}
