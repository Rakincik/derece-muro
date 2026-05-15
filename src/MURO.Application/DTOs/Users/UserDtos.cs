namespace MURO.Application.DTOs.Users;

public record UserListDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    string? StudentType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<string>? GroupNames = null,
    string? Password = null
);

public record UserDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    string? StudentType,
    DateTime? DemoExpiresAt,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<UserGroupDto> Groups,
    List<UserCourseDto> Courses,
    string? Password = null
);

public record UserGroupDto(Guid GroupId, string GroupName);
public record UserCourseDto(Guid CourseId, string CourseTitle, string Mode);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Phone,
    string Role,
    string? StudentType,
    DateTime? DemoExpiresAt
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? Phone,
    string? Role,
    string? StudentType,
    DateTime? DemoExpiresAt,
    bool? IsActive
);

public record BulkCreateUserRequest(List<CreateUserRequest> Users);

public record AssignUserGroupRequest(Guid GroupId);
public record AssignUserCourseRequest(Guid CourseId, string Mode);
