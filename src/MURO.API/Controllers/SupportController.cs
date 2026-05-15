using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Support;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/support")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ISupportService _supportService;
    private readonly ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public SupportController(ISupportService supportService, ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _supportService = supportService;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() => _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException();
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // --- Tickets ---
    [HttpGet("tickets")]
    public async Task<ActionResult<PagedResult<TicketListDto>>> GetTickets(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
        => Ok(await _supportService.GetTicketsAsync(GetTenantId(), page, pageSize, status));

    [HttpGet("tickets/{id:guid}")]
    public async Task<ActionResult<TicketDetailDto>> GetTicket(Guid id)
        => Ok(await _supportService.GetTicketByIdAsync(GetTenantId(), id));

    [HttpPost("tickets")]
    public async Task<ActionResult<TicketListDto>> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var t = await _supportService.CreateTicketAsync(GetTenantId(), GetUserId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "Ticket", t.Id.ToString(), request.Subject, null, GetIp()));
        return Created($"/api/v1/support/tickets/{t.Id}", t);
    }

    [HttpPost("tickets/{ticketId:guid}/reply")]
    public async Task<ActionResult<TicketMessageDto>> Reply(Guid ticketId, [FromBody] ReplyTicketRequest request)
        => Ok(await _supportService.ReplyAsync(GetTenantId(), ticketId, GetUserId(), request));

    [HttpPut("tickets/{ticketId:guid}/close")]
    public async Task<IActionResult> CloseTicket(Guid ticketId)
    {
        await _supportService.CloseTicketAsync(GetTenantId(), ticketId);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Close", "Ticket", ticketId.ToString(), null, null, GetIp()));
        return NoContent();
    }

    // --- FAQ ---
    [HttpGet("faq")]
    public async Task<ActionResult<List<FaqDto>>> GetFaqs()
        => Ok(await _supportService.GetFaqsAsync(GetTenantId()));

    [HttpPost("faq")]
    public async Task<ActionResult<FaqDto>> CreateFaq([FromBody] CreateFaqRequest request)
    {
        var f = await _supportService.CreateFaqAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "FAQ", f.Id.ToString(), request.QuestionText, null, GetIp()));
        return Created($"/api/v1/support/faq/{f.Id}", f);
    }

    [HttpPut("faq/{id:guid}")]
    public async Task<ActionResult<FaqDto>> UpdateFaq(Guid id, [FromBody] UpdateFaqRequest request)
        => Ok(await _supportService.UpdateFaqAsync(GetTenantId(), id, request));

    [HttpDelete("faq/{id:guid}")]
    public async Task<IActionResult> DeleteFaq(Guid id)
    {
        await _supportService.DeleteFaqAsync(GetTenantId(), id);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Delete", "FAQ", id.ToString(), null, null, GetIp()));
        return NoContent();
    }
}
