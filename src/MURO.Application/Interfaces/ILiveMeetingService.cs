using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;

namespace MURO.Application.Interfaces;

public interface ILiveMeetingService
{
    Task<SessionStartResult> StartSessionAsync(Guid tenantId, Guid courseId, Guid sessionId, Guid moderatorUserId);
    Task<SessionJoinResult> JoinSessionAsync(Guid tenantId, Guid courseId, Guid sessionId, Guid userId, string fullName, bool checkGroupAccess = false);
    Task EndSessionAsync(Guid tenantId, Guid courseId, Guid sessionId);
}
