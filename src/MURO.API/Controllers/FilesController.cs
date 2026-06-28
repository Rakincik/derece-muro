using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using System.IO;

namespace MURO.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public FilesController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet("download")]
        [AllowAnonymous] // Herkese açık ama path Base64 kodlu
        public IActionResult DownloadFile([FromQuery] string path, [FromQuery] string? name)
        {
            try
            {
                var decodedBytes = Convert.FromBase64String(path);
                var relativePath = Encoding.UTF8.GetString(decodedBytes);

                // Temel güvenlik kontrolü: Üst dizinlere erişimi engelle
                if (relativePath.Contains("..")) return Forbid();

                // Sadece uploads klasöründen indirmeye izin ver
                if (!relativePath.StartsWith("/uploads")) return BadRequest();

                var fullPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), relativePath.TrimStart('/'));

                if (!System.IO.File.Exists(fullPath)) return NotFound(new { error = "File not found", requestedPath = relativePath, evaluatedPath = fullPath });

                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(fullPath, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                // İndirilecek dosyanın adını belirle (verilmemişse diskteki adını kullan)
                var downloadName = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(fullPath) : name;

                return PhysicalFile(fullPath, contentType, downloadName, enableRangeProcessing: true);
            }
            catch
            {
                return BadRequest();
            }
        }

        [HttpGet("disk-usage")]
        [Authorize] // Sadece giriş yapmış kullanıcılar görebilsin
        public IActionResult GetDiskUsage()
        {
            try
            {
                var root = Path.GetPathRoot(Directory.GetCurrentDirectory());
                var drive = new DriveInfo(root ?? "/");

                long totalSpace = drive.TotalSize;
                long freeSpace = drive.AvailableFreeSpace;
                long usedSpace = totalSpace - freeSpace;
                double percentage = (double)usedSpace / totalSpace * 100;

                return Ok(new
                {
                    TotalSpace = totalSpace,
                    FreeSpace = freeSpace,
                    UsedSpace = usedSpace,
                    Percentage = Math.Round(percentage, 2)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Disk bilgisi alinamadi.", Error = ex.Message });
            }
        }
    }
}
