using MURO.Application.DTOs;
using MURO.Application.DTOs.Questions;

namespace MURO.Application.Interfaces;

public interface IQuestionService
{
    Task<PagedResult<QuestionDto>> GetQuestionsAsync(Guid tenantId, int page, int pageSize, string? status, Guid? instructorId);
    Task<QuestionDto> GetByIdAsync(Guid tenantId, Guid questionId);
    Task<QuestionDto> AskAsync(Guid tenantId, Guid userId, CreateQuestionRequest request);
    Task<QuestionDto> AnswerAsync(Guid tenantId, Guid questionId, AnswerQuestionRequest request);
    Task<QuestionDto> UpdateNoteAsync(Guid tenantId, Guid questionId, Guid userId, UpdateNoteRequest request);
}
