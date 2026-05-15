# Mimari Tasarım

## Katman Diyagramı

```
┌──────────────────────────────────────────────────────┐
│                    MURO.API                          │
│  ┌──────────┐  ┌─────────────┐  ┌────────────────┐  │
│  │Controller│  │ Middleware  │  │  Extensions    │  │
│  │  (29)    │  │   (9)       │  │ (ServiceExt)   │  │
│  └────┬─────┘  └─────────────┘  └────────────────┘  │
├───────┼──────────────────────────────────────────────┤
│       │         MURO.Application                     │
│  ┌────▼─────┐  ┌─────────────┐  ┌────────────────┐  │
│  │Interface │  │    DTOs      │  │  Validators    │  │
│  │  (29)    │  │   (17)      │  │ (FluentValid.) │  │
│  └────┬─────┘  └─────────────┘  └────────────────┘  │
├───────┼──────────────────────────────────────────────┤
│       │         MURO.Infrastructure                  │
│  ┌────▼─────┐  ┌─────────────┐                       │
│  │ Service  │  │ DbContext   │                       │
│  │  (30)    │  │ Migrations  │                       │
│  └────┬─────┘  └──────┬──────┘                       │
├───────┼────────────────┼─────────────────────────────┤
│       │        MURO.Domain                           │
│  ┌────▼─────┐  ┌──────▼──────┐  ┌────────────────┐  │
│  │ Entity   │  │   Enum      │  │  Exceptions    │  │
│  │  (35)    │  │   (8)       │  │   (7 tip)      │  │
│  └──────────┘  └─────────────┘  └────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Bağımlılık Kuralları

- **Domain** → Hiçbir şeye bağımlı değil (saf C# sınıfları)
- **Application** → Sadece Domain'e bağımlı
- **Infrastructure** → Application + Domain'e bağımlı
- **API** → Hepsine bağımlı (composition root)

> ⚠️ Infrastructure asla Application'ın interface'lerine implementation dışında bağımlı olmamalı.

## Dış Bağımlılıklar

```
MURO.API
  ├── PostgreSQL     → Ana veritabanı (EF Core)
  ├── Redis          → Cache + Refresh Token + SignalR backplane
  ├── BigBlueButton  → Canlı ders (REST API + Webhook)
  ├── BunnyCDN       → Video hosting + HLS streaming
  ├── Gemini AI      → AI Podcast üretimi
  ├── Edge TTS       → Metin → Ses dönüşümü
  └── SMTP           → E-posta bildirimleri
```

## Multi-Tenant Mimarisi

Her istek `TenantMiddleware` tarafından işlenir:

1. JWT token'dan veya `X-Tenant-Id` header'ından tenant ID çıkarılır
2. `ITenantService.CurrentTenantId` set edilir
3. Tüm servisler bu tenant ID'yi kullanarak sorgu yapar
4. Cache key'leri `{tenantId}:{servis}:{id}` formatındadır

**Veri izolasyonu:** Tek DB, tenant bazlı filtreleme (shared database, shared schema).

## Middleware Pipeline Sırası

```
İstek → Compression → CorrelationId → SecurityHeaders → GlobalException
      → RequestTiming → RateLimiter → Serilog → InputSanitization
      → CookieAuth → IpBlacklist → Authentication → TenantMiddleware
      → Authorization → SessionValidation → Controller
```
