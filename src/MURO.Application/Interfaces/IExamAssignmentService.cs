using MURO.Application.DTOs.Exams;

namespace MURO.Application.Interfaces;

public interface IExamAssignmentService
{
    // Atamalar
    Task<ExamAssignmentDto> AssignExamAsync(Guid tenantId, Guid examId, CreateExamAssignmentRequest request);
    Task RemoveAssignmentAsync(Guid tenantId, Guid examId, Guid assignmentId);
}
