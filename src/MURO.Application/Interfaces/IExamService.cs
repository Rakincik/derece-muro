using MURO.Application.DTOs;
using MURO.Application.DTOs.Exams;

namespace MURO.Application.Interfaces;

public interface IExamService
{
    // CRUD
    Task<PagedResult<ExamListDto>> GetExamsAsync(Guid tenantId, int page, int pageSize, string? search, string? examType, string? status);
    Task<ExamDetailDto> GetExamByIdAsync(Guid tenantId, Guid examId);
    Task<ExamListDto> CreateExamAsync(Guid tenantId, CreateExamRequest request);
    Task<ExamListDto> UpdateExamAsync(Guid tenantId, Guid examId, UpdateExamRequest request);
    Task DeleteExamAsync(Guid tenantId, Guid examId);

    // Cevap Anahtarı
    Task<ExamDetailDto> UpdateAnswerKeyAsync(Guid tenantId, Guid examId, UpdateAnswerKeyRequest request);

    // Durum
    Task<ExamListDto> UpdateStatusAsync(Guid tenantId, Guid examId, UpdateExamStatusRequest request);

    // PDF
    Task<ExamDetailDto> UpdatePdfAsync(Guid tenantId, Guid examId, UpdateExamPdfRequest request);

}
