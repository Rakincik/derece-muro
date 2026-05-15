using MURO.Application.DTOs;
using MURO.Application.DTOs.Assignments;

namespace MURO.Application.Interfaces;

public interface IAssignmentService
{
    Task<PagedResult<AssignmentListDto>> GetAssignmentsAsync(Guid tenantId, int page, int pageSize, Guid? courseId);
    Task<AssignmentDetailDto> GetAssignmentByIdAsync(Guid tenantId, Guid assignmentId);
    Task<AssignmentListDto> CreateAssignmentAsync(Guid tenantId, CreateAssignmentRequest request);
    Task<AssignmentListDto> UpdateAssignmentAsync(Guid tenantId, Guid assignmentId, UpdateAssignmentRequest request);
    Task DeleteAssignmentAsync(Guid tenantId, Guid assignmentId);

    // Submissions
    Task<SubmissionDto> SubmitAsync(Guid tenantId, Guid assignmentId, Guid userId, SubmitAssignmentRequest request);
    Task<SubmissionDto> GradeSubmissionAsync(Guid tenantId, Guid assignmentId, Guid submissionId, GradeSubmissionRequest request);

    // Öğrenci
    Task<List<MyAssignmentDto>> GetMyAssignmentsAsync(Guid tenantId, Guid userId);
}
