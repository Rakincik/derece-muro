namespace MURO.Domain.Enums;

/// <summary>
/// Canlı ders platformu seçenekleri.
/// Tenant bazlı ayarlanır — her kurum farklı provider kullanabilir.
/// </summary>
public enum MeetingProvider
{
    /// <summary>BigBlueButton — Tam özellikli (kayıt, whiteboard, breakout)</summary>
    Bbb = 0,

    /// <summary>Jitsi Meet — Hafif, hızlı kurulumlu alternatif</summary>
    Jitsi = 1
}
