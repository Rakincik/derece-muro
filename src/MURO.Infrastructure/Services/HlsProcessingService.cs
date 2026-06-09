using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using System.Diagnostics;

namespace MURO.Infrastructure.Services;

/// <summary>
/// FFmpeg kullanarak MP4 → 480p + 720p HLS dönüşümü yapar.
/// Hem BBB kayıtları hem dışarıdan yüklenen videolar bu servisi kullanır.
/// </summary>
public interface IHlsProcessingService
{
    Task<HlsProcessResult> ProcessAsync(Guid assetId, string sourceMp4Path, string outputBaseDir, CancellationToken ct = default);
    Task<HlsProcessResult> ProcessAsync(Guid assetId, string sourceMp4Path, string outputBaseDir, string pipelineType, CancellationToken ct = default);
}

public class TranscodeProgress
{
    public int Percentage { get; set; }
    public double Speed { get; set; }
    public int EtaSeconds { get; set; }
}

public record HlsProcessResult(
    string MasterPlaylistPath,   // /hls/{assetId}/master.m3u8
    string ThumbnailPath,        // /hls/{assetId}/thumbnail.jpg
    bool Success,
    int? DurationSeconds = null,
    string? ErrorMessage = null
);

public class HlsProcessingService : IHlsProcessingService
{
    private readonly ILogger<HlsProcessingService> _logger;
    private readonly string _ffmpegPath;
    private readonly ICacheService _cache;

    public HlsProcessingService(IConfiguration config, ILogger<HlsProcessingService> logger, ICacheService cache)
    {
        _logger = logger;
        _ffmpegPath = config["Ffmpeg:Path"] ?? "ffmpeg"; // PATH'te varsa sadece "ffmpeg"
        _cache = cache;
    }

    public async Task<HlsProcessResult> ProcessAsync(
        Guid assetId, string sourceMp4Path, string outputBaseDir, CancellationToken ct = default)
        => await ProcessAsync(assetId, sourceMp4Path, outputBaseDir, pipelineType: "nvenc", ct);

    public async Task<HlsProcessResult> ProcessAsync(
        Guid assetId, string sourceMp4Path, string outputBaseDir, string pipelineType, CancellationToken ct = default)
    {
        var assetDir = Path.Combine(outputBaseDir, assetId.ToString());
        Directory.CreateDirectory(Path.Combine(assetDir, "480p"));
        Directory.CreateDirectory(Path.Combine(assetDir, "720p"));

        var masterPath    = Path.Combine(assetDir, "master.m3u8");
        var thumbPath     = Path.Combine(assetDir, "thumbnail.jpg");
        var playlist480   = Path.Combine(assetDir, "480p", "index.m3u8");
        var playlist720   = Path.Combine(assetDir, "720p", "index.m3u8");
        var segments480   = Path.Combine(assetDir, "480p", "seg%03d.ts");
        var segments720   = Path.Combine(assetDir, "720p", "seg%03d.ts");

        // ── 1. Thumbnail (30. saniyeden) ──────────────────────────────────────
        _logger.LogInformation("Thumbnail üretiliyor → {AssetId}", assetId);
        var thumbResult = await RunFfmpegAsync(
            $"-y -ss 30 -i \"{sourceMp4Path}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
            null, null, ct);

        if (!thumbResult && !File.Exists(thumbPath))
        {
            // 30. saniye yok ise ilk kareden al
            await RunFfmpegAsync(
                $"-y -i \"{sourceMp4Path}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
                null, null, ct);
        }

        // ── 1.5. Thumbnail Sprites (VTT) ──────────────────────────────────────
        var duration = await GetVideoDurationAsync(sourceMp4Path, ct);
        if (duration > 0)
        {
            _logger.LogInformation("Thumbnail VTT/Sprite üretiliyor → {AssetId}", assetId);
            var spritePattern = Path.Combine(assetDir, "sprite-%03d.jpg");
            var spriteVtt = Path.Combine(assetDir, "thumbnails.vtt");

            var spriteResult = await RunFfmpegAsync(
                $"-y -i \"{sourceMp4Path}\" -vf \"fps=1/10,scale=160:90,tile=10x10\" \"{spritePattern}\"",
                null, null, ct);

            if (spriteResult)
            {
                await GenerateThumbnailsVttAsync(spriteVtt, duration);
            }
        }

        // ── 2. HLS Transcoding ───────────────────────────────────────────────
        var pipelineLabel = pipelineType switch
        {
            "nvenc" => "GPU/NVENC",
            "qsv"   => "GPU/Intel QSV",
            _       => "CPU/libx264"
        };
        _logger.LogInformation("HLS işleniyor ({Pipeline} ile) → {AssetId}", pipelineLabel, assetId);
        
        // var_stream_map kullandığımız için ffmpeg dinamik klasör oluşturacaktır (%v)
        var segmentsPattern = Path.Combine(assetDir, "%v", "seg%03d.ts").Replace("\\", "/");
        var playlistPattern = Path.Combine(assetDir, "%v", "index.m3u8").Replace("\\", "/");
        
        string ffmpegArgs;
        
        if (pipelineType == "nvenc")
        {
            // ── GPU Pipeline: CUDA decode → scale_cuda → NVENC encode (zero-copy, MAX SPEED) ──
            ffmpegArgs = $"-y -hwaccel cuda -hwaccel_output_format cuda -i \"{sourceMp4Path}\" " +
                         $"-filter_complex \"[0:v]scale_cuda=854:480[v1];[0:v]scale_cuda=1280:720[v2]\" " +
                         $"-map \"[v1]\" -c:v:0 h264_nvenc -preset p4 -rc vbr -cq 28 -b:v:0 0.25M -maxrate:v:0 0.25M -bufsize:v:0 0.5M " +
                         $"-map \"[v2]\" -c:v:1 h264_nvenc -preset p4 -rc vbr -cq 28 -b:v:1 0.5M -maxrate:v:1 0.5M -bufsize:v:1 1.0M " +
                         $"-map a:0 -c:a:0 aac -b:a:0 96k " +
                         $"-map a:0 -c:a:1 aac -b:a:1 128k " +
                         $"-f hls -hls_time 6 -hls_playlist_type vod -hls_flags independent_segments " +
                         $"-var_stream_map \"v:0,a:0,name:480p v:1,a:1,name:720p\" " +
                         $"-hls_segment_filename \"{segmentsPattern}\" " +
                         $"\"{playlistPattern}\"";
        }
        else if (pipelineType == "qsv")
        {
            // ── QSV Pipeline: Intel iGPU Hardware Decode & Encode ──
            ffmpegArgs = $"-y -init_hw_device qsv=hw:/dev/dri/renderD129 -filter_hw_device hw " +
                         $"-i \"{sourceMp4Path}\" " +
                         $"-filter_complex \"[0:v]format=nv12,hwupload=extra_hw_frames=64,split=2[s0][s1];" +
                         $"[s0]scale_qsv=854:480[v1];[s1]scale_qsv=1280:720[v2]\" " +
                         $"-map \"[v1]\" -c:v:0 h264_qsv -preset medium -rc vbr -b:v:0 0.25M -maxrate:v:0 0.25M -bufsize:v:0 0.5M " +
                         $"-map \"[v2]\" -c:v:1 h264_qsv -preset medium -rc vbr -b:v:1 0.5M -maxrate:v:1 0.5M -bufsize:v:1 1.0M " +
                         $"-map a:0 -c:a:0 aac -b:a:0 96k " +
                         $"-map a:0 -c:a:1 aac -b:a:1 128k " +
                         $"-f hls -hls_time 6 -hls_playlist_type vod -hls_flags independent_segments " +
                         $"-var_stream_map \"v:0,a:0,name:480p v:1,a:1,name:720p\" " +
                         $"-hls_segment_filename \"{segmentsPattern}\" " +
                         $"\"{playlistPattern}\"";
        }
        else
        {
            // ── CPU Pipeline: Software decode → scale → libx264 encode (6 threads, FAST) ──
            ffmpegArgs = $"-y -i \"{sourceMp4Path}\" " +
                         $"-filter_complex \"[0:v]split=2[v480][v720];[v480]scale=854:480[v1];[v720]scale=1280:720[v2]\" " +
                         $"-map \"[v1]\" -c:v:0 libx264 -preset veryfast -crf 28 -threads 6 -maxrate:v:0 0.3M -bufsize:v:0 0.6M " +
                         $"-map \"[v2]\" -c:v:1 libx264 -preset veryfast -crf 28 -threads 6 -maxrate:v:1 0.6M -bufsize:v:1 1.2M " +
                         $"-map a:0 -c:a:0 aac -b:a:0 96k " +
                         $"-map a:0 -c:a:1 aac -b:a:1 128k " +
                         $"-f hls -hls_time 6 -hls_playlist_type vod -hls_flags independent_segments " +
                         $"-var_stream_map \"v:0,a:0,name:480p v:1,a:1,name:720p\" " +
                         $"-hls_segment_filename \"{segmentsPattern}\" " +
                         $"\"{playlistPattern}\"";
        }

        var ok = await RunFfmpegAsync(ffmpegArgs, assetId, duration > 0 ? duration : null, ct);

        if (!ok)
            return new HlsProcessResult("", "", false, null, $"FFmpeg {pipelineLabel} işlemi başarısız oldu.");

        // ── 3. Master Playlist (ABR) ──────────────────────────────────────────
        var master = new System.Text.StringBuilder();
        master.AppendLine("#EXTM3U");
        master.AppendLine("#EXT-X-STREAM-INF:BANDWIDTH=700000,RESOLUTION=854x480,NAME=\"480p\"");
        master.AppendLine("480p/index.m3u8");
        master.AppendLine("#EXT-X-STREAM-INF:BANDWIDTH=1200000,RESOLUTION=1280x720,NAME=\"720p\"");
        master.AppendLine("720p/index.m3u8");
        
        await File.WriteAllTextAsync(masterPath, master.ToString(), ct);

        // Web-erişilebilir path'ler (wwwroot relative)
        var webMaster = $"/hls/{assetId}/master.m3u8";
        var webThumb  = File.Exists(thumbPath) ? $"/hls/{assetId}/thumbnail.jpg" : null;

        _logger.LogInformation("HLS dönüşümü tamamlandı ✓ AssetId: {AssetId}", assetId);

        return new HlsProcessResult(webMaster, webThumb ?? "", true, duration > 0 ? (int)duration : null);
    }

    private async Task<bool> RunFfmpegAsync(string arguments, Guid? assetId, double? totalDuration, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // FFmpeg progress loglarını arka planda oku (buffer dolmasın)
            using var reader = process.StandardError;
            var stderrTask = Task.Run(async () =>
            {
                var buffer = new char[256];
                int read;
                var currentLine = new System.Text.StringBuilder();
                
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        char c = buffer[i];
                        if (c == '\r' || c == '\n')
                        {
                            if (currentLine.Length > 0)
                            {
                                var line = currentLine.ToString();
                                currentLine.Clear();
                                
                                if (assetId.HasValue && totalDuration.HasValue && totalDuration.Value > 0)
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(line, @"time=(\d+):(\d+):(\d+\.\d+)");
                                    if (match.Success)
                                    {
                                        try
                                        {
                                            var h = int.Parse(match.Groups[1].Value);
                                            var m = int.Parse(match.Groups[2].Value);
                                            var s = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                                            var currentTime = h * 3600 + m * 60 + s;
                                            var percentage = (int)Math.Clamp((currentTime / totalDuration.Value) * 100, 0, 100);
                                            
                                            double speed = 0;
                                            var sm = System.Text.RegularExpressions.Regex.Match(line, @"speed=\s*([\d.]+)x");
                                            if (sm.Success) double.TryParse(sm.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out speed);
                                            int etaSeconds = 0;
                                            if (speed > 0.01) { var rem = totalDuration.Value - currentTime; etaSeconds = (int)Math.Max(0, rem / speed); }
                                            var payload = new TranscodeProgress { Percentage = percentage, Speed = Math.Round(speed, 1), EtaSeconds = etaSeconds };
                                            await _cache.SetAsync($"muro:upload:progress:{assetId}", payload, TimeSpan.FromHours(1));
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        else
                        {
                            currentLine.Append(c);
                        }
                    }
                }
            }, ct);

            await process.WaitForExitAsync(ct);
            await stderrTask;

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FFmpeg çalıştırılırken hata: {Arguments}", arguments[..Math.Min(100, arguments.Length)]);
            return false;
        }
    }

    private async Task<double> GetVideoDurationAsync(string sourceMp4Path, CancellationToken ct)
    {
        try
        {
            // FFmpeg path üzerinden ffprobe path'ini güvenli şekilde bul
            var dir = Path.GetDirectoryName(_ffmpegPath);
            var ext = Path.GetExtension(_ffmpegPath);
            var ffprobeName = string.IsNullOrEmpty(ext) ? "ffprobe" : "ffprobe" + ext;
            var ffprobePath = string.IsNullOrEmpty(dir) ? ffprobeName : Path.Combine(dir, ffprobeName);

            var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{sourceMp4Path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFprobe ile süre alınamadı.");
        }
        return 0;
    }

    private async Task GenerateThumbnailsVttAsync(string vttFilePath, double durationSec)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        int frameCount = (int)Math.Ceiling(durationSec / 10.0);
        int cols = 10;
        int rows = 10;
        int framesPerSprite = cols * rows;

        for (int i = 0; i < frameCount; i++)
        {
            double start = i * 10.0;
            double end = Math.Min((i + 1) * 10.0, durationSec);

            TimeSpan startTime = TimeSpan.FromSeconds(start);
            TimeSpan endTime = TimeSpan.FromSeconds(end);

            int spriteIndex = (i / framesPerSprite) + 1;
            int indexInSprite = i % framesPerSprite;
            int col = indexInSprite % cols;
            int row = indexInSprite / cols;

            int x = col * 160;
            int y = row * 90;

            sb.AppendLine($"{startTime:hh\\:mm\\:ss\\.fff} --> {endTime:hh\\:mm\\:ss\\.fff}");
            sb.AppendLine($"sprite-{spriteIndex:D3}.jpg#xywh={x},{y},160,90");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(vttFilePath, sb.ToString());
    }
}
