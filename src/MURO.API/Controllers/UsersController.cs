using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Users;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Services;

namespace MURO.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin,SuperAdmin,Instructor,Assistant")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    

    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin,Instructor,Assistant")]
    public async Task<ActionResult<PagedResult<UserListDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        var result = await _userService.GetUsersAsync(page, pageSize, search, role, sortBy, sortDir);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin,Instructor,Assistant")]
    public async Task<ActionResult<UserDetailDto>> GetUser(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        return Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin,Instructor,Assistant")]
    public async Task<ActionResult<UserListDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(request);
        return Created($"/api/v1/users/{user.Id}", user);
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,SuperAdmin,Instructor,Assistant")]
    public async Task<ActionResult<BulkImportResultDto>> BulkCreateUsers([FromBody] BulkCreateUserRequest request)
    {
        var result = await _userService.BulkCreateUsersAsync(request.Users);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserListDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (actorRole == "Student" && actorId != id.ToString())
            return Forbid();

        var user = await _userService.UpdateUserAsync(id, request, actorRole);
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        await _userService.DeleteUserAsync(id);
        return NoContent();
    }

    [HttpDelete("bulk")]
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDeleteUsers([FromBody] List<Guid> userIds)
    {
        await _userService.BulkDeleteUsersAsync(userIds);
        return NoContent();
    }

    [HttpPost("{id:guid}/groups")]
    public async Task<IActionResult> AssignToGroup(Guid id, [FromBody] AssignUserGroupRequest request)
    {
        await _userService.AssignToGroupAsync(id, request.GroupId);
        return Ok(new { message = "Kullanıcı gruba eklendi." });
    }

    [HttpGet("{id:guid}/courses/direct")]
    public async Task<ActionResult<List<MURO.Application.DTOs.Courses.CourseListDto>>> GetDirectCourses(
        Guid id, 
        [FromServices] ICourseEnrollmentService enrollmentService)
    {
        var courses = await enrollmentService.GetDirectCoursesByUserAsync(id);
        return Ok(courses);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportUsers([FromQuery] string? role = null)
    {
        var csvBytes = await _userService.ExportUsersAsync(role);
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
        var result = await _userService.GetUsersAsync(1, 1000, null, null, null, null);
        var users = result.Items.Where(u => request.UserIds == null || request.UserIds.Contains(u.Id)).ToList();
        
        var exportData = users.Select(u => new UserExportDto
        {
            KayitTarihi = u.CreatedAt.ToString("dd.MM.yyyy"),
            Ad = u.FirstName,
            Soyad = u.LastName ?? "",
            KullaniciAdi = u.Email,
            Eposta = u.Email,
            TC = u.TcNo ?? "",
            Telefon = u.Phone ?? ""
        }).ToList();

        var excelBytes = excelService.ExportToExcel(exportData, "Kullanicilar");
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"kullanicilar_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpPost("import-excel")]
    public async Task<IActionResult> ImportUsersExcel(
        IFormFile file,
        [FromQuery] Guid? groupId,
        [FromServices] IExcelService excelService,
        [FromServices] IGroupService groupService)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Lütfen bir dosya seçin." });

        using var stream = file.OpenReadStream();
        var importedUsers = excelService.ImportFromExcel<UserImportDto>(stream);

        var requests = new List<CreateUserRequest>();
        foreach (var u in importedUsers)
        {
            if (string.IsNullOrWhiteSpace(u.Ad))
                continue;

            var rawPhone = u.Telefon?.Trim() ?? "";
            var phone = UserService.CleanPhoneNumber(rawPhone) ?? "";
            
            var fixedRole = u.Rol?.Trim() ?? "";
            var role = fixedRole.ToLower() == "admin" ? "Admin" : 
                       fixedRole.ToLower() == "öğretmen" ? "Instructor" : "Student";

            var tc = u.TC?.Trim() ?? "";

            var fn = string.IsNullOrWhiteSpace(u.Ad) ? "user" : u.Ad.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "user";
            fn = fn.Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ı", "i").Replace("ö", "o").Replace("ç", "c");
            
            var ln = string.IsNullOrWhiteSpace(u.Soyad) ? "x" : u.Soyad.Trim().ToLowerInvariant();
            ln = ln.Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ı", "i").Replace("ö", "o").Replace("ç", "c");
            var lChar = string.IsNullOrEmpty(ln) ? "x" : ln.Substring(0, 1);
            
            var pStr = new string(phone.Where(char.IsDigit).ToArray());
            var pLast2 = pStr.Length >= 2 ? pStr.Substring(pStr.Length - 2) : "00";
            
            var password = $"{fn}.{pLast2}.{lChar}";

            requests.Add(new CreateUserRequest(
                u.Ad.Trim(),
                u.Soyad?.Trim() ?? "",
                u.Eposta?.Trim() ?? "", // Use the Eposta from Excel or empty
                null, // Username will be generated if null
                password,
                phone,
                role,
                role == "Student" ? "Active" : null,
                null,
                tc
            ));
        }

        var importResult = await _userService.BulkCreateUsersAsync(requests);

        if (groupId.HasValue && importResult.ImportedCount > 0)
        {
            var newMemberIds = importResult.Details
                .Where(d => d.Status == "Başarılı" && d.UserId.HasValue)
                .Select(d => d.UserId.Value)
                .ToList();

            if (newMemberIds.Any())
            {
                await groupService.AddMembersAsync(groupId.Value, newMemberIds);
            }
        }

        return Ok(new { 
            message = $"{importResult.ImportedCount} kullanıcı başarıyla eklendi, {importResult.FailedCount} kişi başarısız oldu.", 
            importedCount = importResult.ImportedCount,
            skippedCount = importResult.FailedCount,
            details = importResult.Details
        });
    }

    [HttpPost("bulk-reset-passwords")]
    [AllowAnonymous] // ONLY TEMPORARY, WILL REMOVE AFTER EXECUTION
    public async Task<IActionResult> BulkResetPasswords(
        [FromServices] MURO.Infrastructure.Persistence.MuroDbContext dbContext)
    {
        var users = dbContext.Users.Where(u => u.Role == MURO.Domain.Enums.UserRole.Student).ToList();
        int count = 0;
        foreach (var u in users)
        {
            var fn = string.IsNullOrWhiteSpace(u.FirstName) ? "user" : u.FirstName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "user";
            fn = fn.Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ı", "i").Replace("ö", "o").Replace("ç", "c");
            
            var ln = string.IsNullOrWhiteSpace(u.LastName) ? "x" : u.LastName.Trim().ToLowerInvariant();
            ln = ln.Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ı", "i").Replace("ö", "o").Replace("ç", "c");
            var lChar = string.IsNullOrEmpty(ln) ? "x" : ln.Substring(0, 1);
            
            var rawPhone = u.Phone ?? "";
            var pStr = new string(rawPhone.Where(char.IsDigit).ToArray());
            var pLast2 = pStr.Length >= 2 ? pStr.Substring(pStr.Length - 2) : "00";
            
            var newPassword = $"{fn}.{pLast2}.{lChar}";
            u.PasswordHash = newPassword;
            count++;
        }
        await dbContext.SaveChangesAsync();
        return Ok(new { message = $"{count} öğrencinin şifresi başarıyla güncellendi." });
    }
}

public class UserImportDto
{
    public string? Ad { get; set; }
    public string? Soyad { get; set; }
    public string? TC { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Rol { get; set; }
}

public class UserTemplateDto
{
    public string Ad { get; set; } = "Ahmet";
    public string Soyad { get; set; } = "Yılmaz";
    public string TC { get; set; } = "12345678901";
    public string Telefon { get; set; } = "5321234567";
    public string Eposta { get; set; } = "ahmet.yilmaz@ornek.com";
    public string Rol { get; set; } = "Öğrenci";
}

public class UserExportDto
{
    public string KayitTarihi { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Eposta { get; set; } = string.Empty;
    public string TC { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
}

public class ExportExcelRequest
{
    public List<Guid>? UserIds { get; set; }
}
