namespace MURO.Application.DTOs.Groups;

public record GroupListDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentGroupId,
    string? ParentGroupName,
    string? Color,
    string? EducationType,
    int MemberCount,
    int CourseCount,
    DateTime CreatedAt
);

public record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentGroupId,
    string? ParentGroupName,
    string? Color,
    string? EducationType,
    int MemberCount,
    int CourseCount,
    List<GroupMemberDto> Members,
    List<GroupCourseDto> Courses,
    List<GroupChildDto> Children,
    DateTime CreatedAt
);

public record GroupMemberDto(Guid UserId, string UserFullName, string Email, string Role, DateTime AddedAt);
public record GroupCourseDto(Guid CourseId, string CourseTitle, string Mode);
public record GroupChildDto(Guid Id, string Name, int MemberCount);

public record GroupTreeDto(
    Guid Id,
    string Name,
    int MemberCount,
    List<GroupTreeDto> Children
);

public record CreateGroupRequest(
    string Name,
    string? Description,
    Guid? ParentGroupId,
    string? Color,
    string? EducationType
);

public record UpdateGroupRequest(
    string? Name,
    string? Description,
    Guid? ParentGroupId,
    string? Color,
    string? EducationType
);

public record AddGroupMembersRequest(List<Guid> UserIds);

public record MoveMembersRequest(Guid ToGroupId, List<Guid> UserIds);
