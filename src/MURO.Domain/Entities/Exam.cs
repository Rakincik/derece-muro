namespace MURO.Domain.Entities;

public class Exam
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ExamType { get; set; } = "TYT"; // TYT, AYT, LGS, KPSS_GY, KPSS_GK, KPSS_EB, OABT, ALES, YDS, DGS, Deneme, Quiz, Genel
    public int QuestionCount { get; set; }
    public int OptionCount { get; set; } = 5; // A-E (4 veya 5)
    public int? DurationMinutes { get; set; }
    public double WrongPenaltyWeight { get; set; } = 0.25; // 4 yanlış 1 doğru (0.25) veya 3 yanlış 1 doğru (0.33)
    public double MaxScore { get; set; } = 0; // Taban Puan (e.g. 100)
    public int VirtualParticipantCount { get; set; } = 0; // 0 = devre dışı

    // Soru Puan Katsayıları — JSON: {"1": 1.000, "2": 1.500, ...}
    public string? QuestionWeightsJson { get; set; }

    // Sınav Ders Bölümleri — JSON: [{"name":"Türkçe","start":1,"end":40},{"name":"Mat","start":41,"end":80}]
    public string? SectionsJson { get; set; }

    // PDF Dosyaları
    public string? PdfUrl { get; set; }          // Sınav kitapçığı
    public string? SolutionPdfUrl { get; set; }  // Çözüm kitapçığı

    // Cevap Anahtarı — JSON: {"1":"A","2":"C","3":"B",...}
    public string? AnswerKeyJson { get; set; }

    // Durum & Zamanlama
    public string Status { get; set; } = "Taslak"; // Taslak, Yayında, Tamamlandı
    public bool ShowResults { get; set; } = true;
    public string ResultMode { get; set; } = "immediate"; // immediate, scheduled, manual
    public DateTime? ResultPublishDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<ExamAssignment> ExamAssignments { get; set; } = new List<ExamAssignment>();
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}
