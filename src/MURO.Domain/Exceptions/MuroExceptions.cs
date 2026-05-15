namespace MURO.Domain.Exceptions;

/// <summary>
/// Tüm MURO özel exception'larının base sınıfı.
/// GlobalExceptionMiddleware bu tipi yakalayarak uygun HTTP status code döner.
/// </summary>
public abstract class MuroException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    protected MuroException(string message, int statusCode, string errorCode)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>404 — Kaynak bulunamadı</summary>
public class NotFoundException : MuroException
{
    public NotFoundException(string entity, Guid id)
        : base($"{entity} bulunamadı. (ID: {id})", 404, "RESOURCE_NOT_FOUND") { }

    public NotFoundException(string message)
        : base(message, 404, "RESOURCE_NOT_FOUND") { }
}

/// <summary>400 — Geçersiz istek</summary>
public class BadRequestException : MuroException
{
    public BadRequestException(string message)
        : base(message, 400, "BAD_REQUEST") { }
}

/// <summary>403 — Yetkisiz erişim</summary>
public class ForbiddenException : MuroException
{
    public ForbiddenException(string message = "Bu işlem için yetkiniz yok.")
        : base(message, 403, "FORBIDDEN") { }
}

/// <summary>409 — Çakışma (duplicate kayıt vs.)</summary>
public class ConflictException : MuroException
{
    public ConflictException(string message)
        : base(message, 409, "CONFLICT") { }
}

/// <summary>422 — Doğrulama hatası</summary>
public class ValidationException : MuroException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Bir veya daha fazla doğrulama hatası oluştu.", 422, "VALIDATION_FAILED")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { { field, new[] { error } } }) { }
}

/// <summary>429 — Çok fazla istek</summary>
public class RateLimitedException : MuroException
{
    public RateLimitedException()
        : base("Çok fazla istek gönderdiniz. Lütfen biraz bekleyiniz.", 429, "RATE_LIMITED") { }
}

/// <summary>503 — Servis kullanılamıyor</summary>
public class ServiceUnavailableException : MuroException
{
    public ServiceUnavailableException(string service)
        : base($"{service} servisi şu anda kullanılamıyor.", 503, "SERVICE_UNAVAILABLE") { }
}
