using System.Data;

namespace MURO.Application.Interfaces;

public interface IExcelService
{
    byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Sheet1");
    List<T> ImportFromExcel<T>(Stream stream) where T : new();
    byte[] GenerateTemplate<T>(string sheetName = "Template");
}
