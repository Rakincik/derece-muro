using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Users;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITenantService _tenantService;
    private readonly IAuditService _auditService;

    public UsersController(IUserService userService, ITenantService tenantService, IAuditService auditService)
    {
        _userService = userService;
        _tenantService = tenantService;
        _auditService = auditService;
    }

    private Guid GetTenantId() =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        var result = await _userService.GetUsersAsync(GetTenantId(), page, pageSize, search, role, sortBy, sortDir);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDetailDto>> GetUser(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(GetTenantId(), id);
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserListDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(GetTenantId(), request);
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(GetTenantId(), actorId != null ? Guid.Parse(actorId) : null, null, "Create", "User", user.Id.ToString(), $"{request.FirstName} {request.LastName}", $"Rol: {request.Role}", ip);
        return Created($"/api/v1/users/{user.Id}", user);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<List<UserListDto>>> BulkCreateUsers([FromBody] BulkCreateUserRequest request)
    {
        var users = await _userService.BulkCreateUsersAsync(GetTenantId(), request.Users);
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(GetTenantId(), actorId != null ? Guid.Parse(actorId) : null, null, "BulkCreate", "User", null, null, $"{users.Count} kullanıcı oluşturuldu", ip);
        return Ok(users);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserListDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateUserAsync(GetTenantId(), id, request);
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(GetTenantId(), actorId != null ? Guid.Parse(actorId) : null, null, "Update", "User", id.ToString(), $"{user.FirstName} {user.LastName}", null, ip);
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _userService.DeleteUserAsync(GetTenantId(), id);
        await _auditService.LogAsync(GetTenantId(), actorId != null ? Guid.Parse(actorId) : null, null, "Delete", "User", id.ToString(), null, null, ip);
        return NoContent();
    }

    [HttpDelete("bulk")]
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDeleteUsers([FromBody] List<Guid> userIds)
    {
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _userService.BulkDeleteUsersAsync(GetTenantId(), userIds);
        await _auditService.LogAsync(GetTenantId(), actorId != null ? Guid.Parse(actorId) : null, null, "BulkDelete", "User", null, null, $"{userIds.Count} kullanıcı silindi", ip);
        return NoContent();
    }

    [HttpPost("{id:guid}/groups")]
    public async Task<IActionResult> AssignToGroup(Guid id, [FromBody] AssignUserGroupRequest request)
    {
        await _userService.AssignToGroupAsync(GetTenantId(), id, request.GroupId);
        return Ok(new { message = "Kullanıcı gruba eklendi." });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportUsers([FromQuery] string? role = null)
    {
        var csvBytes = await _userService.ExportUsersAsync(GetTenantId(), role);
        return File(csvBytes, "text/csv", $"kullanicilar_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export-template")]
    public IActionResult ExportTemplate([FromServices] IExcelService excelService)
    {
        var templateData = new List<UserTemplateDto> { new UserTemplateDto() };
        var excelBytes = excelService.ExportToExcel(templateData, "Ogrenciler");
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ogrenci_sablon.xlsx");
    }

    [HttpPost("export-excel")]
    public async Task<IActionResult> ExportUsersExcel(
        [FromBody] ExportExcelRequest request,
        [FromServices] IExcelService excelService)
    {
        var result = await _userService.GetUsersAsync(GetTenantId(), 1, 1000, null, null, null, null);
        var users = result.Items.Where(u => request.UserIds == null || request.UserIds.Contains(u.Id)).ToList();
        
        var exportData = users.Select(u => new UserExportDto
        {
            KayitTarihi = u.CreatedAt.ToString("dd.MM.yyyy"),
            Ad = u.FirstName,
            Soyad = u.LastName ?? "",
            EPosta = u.Email,
            Telefon = u.Phone ?? ""
        }).ToList();

        var excelBytes = excelService.ExportToExcel(exportData, "Kullanicilar");
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"kullanicilar_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpPost("import-excel")]
    public async Task<IActionResult> ImportUsersExcel(
        IFormFile file,
        [FromServices] IExcelService excelService)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Lütfen bir dosya seçin." });

        using var stream = file.OpenReadStream();
        var importedUsers = excelService.ImportFromExcel<UserImportDto>(stream);

        string FixEncoding(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains('Ä') || value.Contains('Ã') || value.Contains('Å') || value.Contains('Ð') || value.Contains('Ý') || value.Contains('Þ') || value.Contains('Ö') || value.Contains('Ü') || value.Contains('Ç') || value.Contains('±'))
            {
                try
                {
                    var bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(value);
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
                catch { }
            }
            return value;
        }

        var requests = new List<CreateUserRequest>();
        foreach (var u in importedUsers)
        {
            if (string.IsNullOrWhiteSpace(u.Email) || string.IsNullOrWhiteSpace(u.Ad))
                continue;

            var phone = u.Telefon?.Trim() ?? "";
            var password = phone.Length >= 6 ? phone.Substring(phone.Length - 6) : new Random().Next(100000, 999999).ToString();
            
            var fixedRole = FixEncoding(u.Rol?.Trim() ?? "");
            var role = fixedRole.ToLower() == "admin" ? "Admin" : 
                       fixedRole.ToLower() == "öğretmen" ? "Instructor" : "Student";

            requests.Add(new CreateUserRequest(
                FixEncoding(u.Ad.Trim()),
                FixEncoding(u.Soyad?.Trim() ?? ""),
                u.Email.Trim(),
                password,
                phone,
                role,
                role == "Student" ? "Aktif" : null,
                null
            ));
        }

        var results = await _userService.BulkCreateUsersAsync(GetTenantId(), requests);

        return Ok(new { message = $"{results.Count} kullanıcı başarıyla eklendi.", importedCount = results.Count });
    }
}

public class UserImportDto
{
    public string? Ad { get; set; }
    public string? Soyad { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Rol { get; set; }
}

public class UserTemplateDto
{
    public string Ad { get; set; } = "Ahmet";
    public string Soyad { get; set; } = "Yılmaz";
    public string Telefon { get; set; } = "5321234567";
    public string Email { get; set; } = "ahmet@ornek.com";
    public string Rol { get; set; } = "Öğrenci";
}

public class UserExportDto
{
    public string KayitTarihi { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string EPosta { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
}

public class ExportExcelRequest
{
    public List<Guid>? UserIds { get; set; }
}
