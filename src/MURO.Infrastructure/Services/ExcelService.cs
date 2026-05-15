using ClosedXML.Excel;
using MURO.Application.Interfaces;
using System.Reflection;

namespace MURO.Infrastructure.Services;

public class ExcelService : IExcelService
{
    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Sheet1")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        var properties = typeof(T).GetProperties();
        for (int i = 0; i < properties.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = properties[i].Name;
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var item in data)
        {
            for (int col = 0; col < properties.Length; col++)
            {
                var val = properties[col].GetValue(item);
                worksheet.Cell(row, col + 1).Value = val?.ToString();
            }
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public List<T> ImportFromExcel<T>(Stream stream) where T : new()
    {
        var list = new List<T>();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1); // Assume the first sheet
        var properties = typeof(T).GetProperties();

        var headerRow = worksheet.Row(1);
        var headers = new Dictionary<string, int>();

        // Map column headers to their 1-based index
        for (int i = 1; i <= properties.Length; i++)
        {
            var cellValue = headerRow.Cell(i).GetString()?.Trim();
            if (!string.IsNullOrEmpty(cellValue))
            {
                headers[cellValue.ToLower()] = i;
            }
        }

        var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header row
        foreach (var row in rows)
        {
            var item = new T();
            bool hasData = false;

            foreach (var property in properties)
            {
                if (headers.TryGetValue(property.Name.ToLower(), out int colIndex))
                {
                    var cell = row.Cell(colIndex);
                    var cellValue = cell.GetString();
                    
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        hasData = true;
                        try
                        {
                            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                            var convertedValue = Convert.ChangeType(cellValue, targetType);
                            property.SetValue(item, convertedValue);
                        }
                        catch
                        {
                            // If conversion fails, ignore for this simple implementation
                        }
                    }
                }
            }

            if (hasData)
            {
                list.Add(item);
            }
        }

        return list;
    }

    public byte[] GenerateTemplate<T>(string sheetName = "Template")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        var properties = typeof(T).GetProperties();
        for (int i = 0; i < properties.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = properties[i].Name;
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
