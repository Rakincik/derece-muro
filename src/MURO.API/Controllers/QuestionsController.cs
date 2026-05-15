using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Questions;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/questions")]
[Authorize]
public class QuestionsController : ControllerBase
{
    private readonly IQuestionService _questionService;
    private readonly Application.Interfaces.ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public QuestionsController(IQuestionService questionService, Application.Interfaces.ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _questionService = questionService;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() => _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException();
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<ActionResult<PagedResult<QuestionDto>>> GetQuestions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] Guid? instructorId = null)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role == "Instructor")
        {
            instructorId = GetUserId();
        }

        return Ok(await _questionService.GetQuestionsAsync(GetTenantId(), page, pageSize, status, instructorId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuestionDto>> GetQuestion(Guid id)
        => Ok(await _questionService.GetByIdAsync(GetTenantId(), id));

    [HttpPost]
    public async Task<ActionResult<QuestionDto>> Ask([FromBody] CreateQuestionRequest request)
    {
        var q = await _questionService.AskAsync(GetTenantId(), GetUserId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Ask", "Question", q.Id.ToString(), request.Subject, null, GetIp()));
        return Created($"/api/v1/questions/{q.Id}", q);
    }

    [HttpPut("{id:guid}/answer")]
    public async Task<ActionResult<QuestionDto>> Answer(Guid id, [FromBody] AnswerQuestionRequest request)
    {
        var q = await _questionService.AnswerAsync(GetTenantId(), id, request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Answer", "Question", id.ToString(), null, null, GetIp()));
        return Ok(q);
    }

    [HttpPatch("{id:guid}/note")]
    public async Task<ActionResult<QuestionDto>> UpdateNote(Guid id, [FromBody] UpdateNoteRequest request)
        => Ok(await _questionService.UpdateNoteAsync(GetTenantId(), id, GetUserId(), request));
}
