namespace MURO.Application.Interfaces;

/// <summary>
/// Google Gemini API ile metni profesyonel podcast script'e çevirir.
/// </summary>
public interface IGeminiService
{
    Task<string> EnhanceToScriptAsync(string rawText);
}
