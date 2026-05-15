using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MURO.Application.DTOs.Admin;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers.Admin;

[EnableRateLimiting("ApiPolicy")]
[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/v1/admin")]
public class AdminJobsController : ControllerBase
{
    private readonly IAdminJobsService _jobsService;

    public AdminJobsController(IAdminJobsService jobsService)
    {
        _jobsService = jobsService;
    }

    [HttpGet("jobs/status")]
    public async Task<IActionResult> GetJobStatus()
    {
        var (status_code, data) = await _jobsService.GetJobStatus();
        return StatusCode(status_code, data);
    }

    [HttpGet("jobs/errors")]
    public async Task<IActionResult> GetErrorQueue()
    {
        var (status_code, data) = await _jobsService.GetErrorQueue();
        return StatusCode(status_code, data);
    }

    [HttpPost("jobs/retry/{id:guid}")]
    public async Task<IActionResult> RetryFailedItem(Guid id, [FromBody] RetryRequest request)
    {
        var (status_code, data) = await _jobsService.RetryFailedItem(id, request);
        return StatusCode(status_code, data);
    }
}
