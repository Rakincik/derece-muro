namespace MURO.API.Middleware;

/// <summary>
/// Response Caching profilleri — Controller'larda [ResponseCache] attribute ile kullanılır.
/// Cache-Control header'ı üzerinden çalışır (CDN, reverse proxy, tarayıcı cache).
/// </summary>
public static class CacheProfiles
{
    /// <summary>Sık değişen listeler (kurs listesi, ödev listesi) — 60 saniye.</summary>
    public const int ShortDuration = 60;

    /// <summary>Orta sıklıkta değişen veriler (FAQ, takvim, analytics) — 5 dakika.</summary>
    public const int MediumDuration = 300;

    /// <summary>Nadiren değişen veriler (statik referans verisi) — 30 dakika.</summary>
    public const int LongDuration = 1800;
}
