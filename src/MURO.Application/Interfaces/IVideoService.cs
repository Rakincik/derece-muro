using MURO.Application.DTOs.Videos;

namespace MURO.Application.Interfaces;

public interface IVideoNoteService
{
    Task<List<VideoNoteDto>> GetMyNotesAsync(Guid userId, Guid mediaAssetId);
    Task<VideoNoteDto> AddNoteAsync(Guid userId, Guid mediaAssetId, CreateVideoNoteRequest request);
    Task<VideoNoteDto> UpdateNoteAsync(Guid userId, Guid noteId, UpdateVideoNoteRequest request);
    Task DeleteNoteAsync(Guid userId, Guid noteId);
}

public interface IVideoProgressService
{
    Task<VideoProgressDto?> GetProgressAsync(Guid userId, Guid mediaAssetId);
    Task<VideoProgressDto?> UpsertProgressAsync(Guid userId, Guid mediaAssetId, UpdateVideoProgressRequest request);
    Task<List<VideoProgressDto>> GetMyCourseProgressAsync(Guid userId, Guid courseId);
}
