using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Accounting;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting("ApiPolicy")]
[ApiController]
[Route("api/v1/accounting")]
[Authorize(Roles = "Admin,SuperAdmin,Assistant,Accountant")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accountingService;
    private readonly IBackgroundJobQueue _jobQueue;

    public AccountingController(IAccountingService accountingService, IBackgroundJobQueue jobQueue)
    {
        _accountingService = accountingService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId()
    {
        var tenantIdStr = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                          ?? User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ─── Dashboard Summary ───────────────────────────────────────────────────

    [HttpGet("summary")]
    public async Task<ActionResult<AccountingSummaryDto>> GetSummary(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var tid = GetTenantId();
        var result = await _accountingService.GetSummaryAsync(tid, from, to);
        return Ok(result);
    }

    // ─── Transactions ────────────────────────────────────────────────────────

    [HttpGet("transactions")]
    public async Task<ActionResult> GetTransactions(
        [FromQuery] string? type, [FromQuery] string? status, [FromQuery] Guid? planId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var tid = GetTenantId();
        var result = await _accountingService.GetTransactionsAsync(tid, type, status, planId, from, to, page, pageSize);
        return Ok(new { data = result });
    }

    [HttpPost("transactions")]
    public async Task<ActionResult> CreateTransaction([FromBody] CreateTransactionRequest req)
    {
        var tid = GetTenantId();
        var result = await _accountingService.CreateTransactionAsync(tid, GetUserId(), req, GetIp());
        return Ok(result);
    }

    [HttpDelete("transactions/{id}")]
    public async Task<ActionResult> DeleteTransaction(Guid id)
    {
        var tid = GetTenantId();
        var result = await _accountingService.DeleteTransactionAsync(tid, GetUserId(), id, GetIp());
        if (!result) return NotFound(new { message = "İşlem bulunamadı." });
        return Ok(new { message = "İşlem iptal edildi." });
    }

    // ─── Plans / Subscriptions ───────────────────────────────────────────────

    [HttpGet("plans")]
    public async Task<ActionResult> GetPlans()
    {
        var tid = GetTenantId();
        var result = await _accountingService.GetPlansAsync(tid);
        return Ok(new { data = result });
    }

    [HttpPost("plans")]
    public async Task<ActionResult> CreatePlan([FromBody] CreatePlanRequest req)
    {
        var tid = GetTenantId();
        var result = await _accountingService.CreatePlanAsync(tid, GetUserId(), req, GetIp());
        return Ok(result);
    }

    [HttpDelete("plans/{id}")]
    public async Task<ActionResult> DeletePlan(Guid id)
    {
        var tid = GetTenantId();
        var result = await _accountingService.DeletePlanAsync(tid, GetUserId(), id, GetIp());
        if (!result) return NotFound(new { message = "Plan bulunamadı veya kullanılıyor." });
        return Ok(new { message = "Plan silindi." });
    }
}
