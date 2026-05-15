namespace MURO.Application.Interfaces;

public interface ICourseEnrollmentService
{
    Task AssignToGroupAsync(Guid tenantId, Guid courseId, Guid groupId, string mode);
    Task RemoveFromGroupAsync(Guid tenantId, Guid courseId, Guid groupId);
}
