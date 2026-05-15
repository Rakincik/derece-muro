using MURO.Application.Interfaces;
using MURO.Domain.Enums;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Tenant ayarlarına göre doğru meeting provider'ı çözer.
/// Varsayılan: BBB. Tenant "Jitsi" seçtiyse JitsiMeetService döner.
/// </summary>
public class MeetingProviderFactory
{
    private readonly BbbMeetingProvider _bbbProvider;
    private readonly JitsiMeetService _jitsiProvider;

    public MeetingProviderFactory(BbbMeetingProvider bbbProvider, JitsiMeetService jitsiProvider)
    {
        _bbbProvider = bbbProvider;
        _jitsiProvider = jitsiProvider;
    }

    /// <summary>Enum'a göre provider seç</summary>
    public IMeetingProvider GetProvider(MeetingProvider provider) => provider switch
    {
        MeetingProvider.Jitsi => _jitsiProvider,
        _ => _bbbProvider // Varsayılan: BBB
    };

    /// <summary>String'e göre provider seç (tenant ayarlarından)</summary>
    public IMeetingProvider GetProvider(string? providerName) => providerName?.ToLowerInvariant() switch
    {
        "jitsi" => _jitsiProvider,
        _ => _bbbProvider // Varsayılan: BBB
    };
}
