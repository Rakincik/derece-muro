namespace MURO.Application.DTOs.Users;

public record UserListDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? Phone,
    string Role,
    string? StudentType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<string>? GroupNames = null,
    string? Password = null,
    string? TcNo = null
);

public record UserDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? Phone,
    string Role,
    string? StudentType,
    DateTime? DemoExpiresAt,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<UserGroupDto> Groups,
    List<UserCourseDto> Courses,
    string? Password = null,
    string? TcNo = null
);

public record UserGroupDto(Guid GroupId, string GroupName);
public record UserCourseDto(Guid CourseId, string CourseTitle, string Mode);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Username,
    string Password,
    string? Phone,
    string Role,
    string? StudentType,
    DateTime? DemoExpiresAt,
    string? TcNo = null
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Username,
    string? Password,
    string? Phone,
    string? Role,
    string? StudentType,
    DateTime? DemoExpiresAt,
    bool? IsActive,
    string? TcNo = null
);

public record BulkCreateUserRequest(List<CreateUserRequest> Users);

public record AssignUserGroupRequest(Guid GroupId);
public record AssignUserCourseRequest(Guid CourseId, string Mode);

public class BulkImportResultDto
{
    public int TotalAttempted { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkImportItemResultDto> Details { get; set; } = new();
}

public class BulkImportItemResultDto
{
    public Guid? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Başarılı" or "Başarısız"
    public string Reason { get; set; } = string.Empty;
}
