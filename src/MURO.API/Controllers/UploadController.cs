using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    public record PresignedUrlRequest(string FileName, string ContentType);
    public record PresignedUrlResponse(string UploadUrl, string PublicUrl);

    [HttpPost("presigned")]
    [RequestFormLimits(MultipartBodyLengthLimit = 256 * 1024 * 1024)]
    public ActionResult<PresignedUrlResponse> GetPresignedUrl([FromBody] PresignedUrlRequest request)
    {
        // Quota Enforcement: StorageLimitGb (Şu an dosya boyutları DB'de tutulmadığı için sadece limit=0 kontrolü yapılabilir)
        var tenantId = User.FindFirst("TenantId")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            var _db = HttpContext.RequestServices.GetService<MURO.Infrastructure.Persistence.MuroDbContext>();
            if (_db != null)
            {
                var tenant = _db.Tenants.FirstOrDefault(t => t.Id == Guid.Parse(tenantId));
                if (tenant != null && tenant.StorageLimitGb.HasValue && tenant.StorageLimitGb.Value <= 0)
                {
                    throw new MURO.Application.Exceptions.QuotaExceededException("Depolama kotanız doldu. Dosya yükleyemezsiniz.");
                }
                // TODO: İleride MediaAsset tablosuna FileSizeBytes eklenince gerçek kullanım hesaplanıp StorageLimitGb ile karşılaştırılacak.
            }
        }
        // For demonstration/demo environments, we generate a mock URL
        // In a real scenario, this would use BunnyCDN or AWS S3 SDK to generate a signed URL
        var fileId = Guid.NewGuid().ToString("N");
        var ext = Path.GetExtension(request.FileName);
        var safeName = $"{fileId}{ext}";
        
        var scheme = HttpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? HttpContext.Request.Scheme;
        var host = HttpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? HttpContext.Request.Host.Value;
        
        // Return a dummy endpoint that will accept the PUT request
        var uploadUrl = $"{scheme}://{host}/api/v1/upload/direct/{safeName}";
        var publicUrl = $"{scheme}://{host}/api/v1/uploads/{safeName}";

        return Ok(new PresignedUrlResponse(uploadUrl, publicUrl));
    }

    [HttpPut("direct/{fileName}")]
    [AllowAnonymous] // Pre-signed URLs are typically open (auth via signature, but here it's mock)
    [RequestFormLimits(MultipartBodyLengthLimit = 256 * 1024 * 1024)]
    public async Task<IActionResult> DirectUpload(string fileName)
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var filePath = Path.Combine(uploadsFolder, fileName);
        
        using var stream = new FileStream(filePath, FileMode.Create);
        await Request.Body.CopyToAsync(stream);

        return Ok(new { message = "Dosya yüklendi" });
    }
}
