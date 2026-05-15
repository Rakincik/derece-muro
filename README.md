# MURO LMS — Backend API

Multi-Tenant uzaktan eğitim platformu. .NET 8 Web API.

## 🚀 Hızlı Başlangıç

### Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/)
- [Redis 7+](https://redis.io/)
- [Docker](https://www.docker.com/) (opsiyonel)

### Kurulum

```bash
# 1. Repo'yu klonla
git clone <repo-url>
cd muro-v2

# 2. Bağımlılıkları yükle
dotnet restore

# 3. Veritabanını oluştur
dotnet ef database update --project src/MURO.API

# 4. Çalıştır
dotnet run --project src/MURO.API
```

API varsayılan olarak `http://localhost:5000` üzerinden çalışır.

### Swagger UI

Development modda: [http://localhost:5000/swagger](http://localhost:5000/swagger)

## 📁 Proje Yapısı

```
src/
├── MURO.API/              # Web API katmanı
│   ├── Controllers/       # 29 API controller
│   ├── Middleware/         # 9 güvenlik middleware
│   ├── Extensions/        # DI servis kayıtları
│   └── Hubs/              # SignalR hub
├── MURO.Application/      # İş mantığı sözleşmeleri
│   ├── Interfaces/        # 29 servis interface
│   ├── DTOs/              # Request/Response modelleri
│   └── Validators/        # FluentValidation kuralları
├── MURO.Infrastructure/   # Implementasyonlar
│   ├── Services/          # 30 servis implementasyonu
│   ├── Persistence/       # EF Core DbContext + Migrations
│   └── ...
├── MURO.Domain/           # Entity ve Enum tanımları
│   ├── Entities/          # 35 domain entity
│   ├── Enums/             # 8 enum
│   └── Exceptions/        # Custom exception hiyerarşisi
└── MURO.Worker/           # Arka plan servisleri
```

## 🔒 Güvenlik

- JWT Bearer Authentication (2 saat access token + 7 gün refresh token)
- 9 katmanlı middleware pipeline (SecurityHeaders, RateLimiter, IpBlacklist, InputSanitization, vb.)
- Brute-force koruması (5 deneme → hesap kilidi)
- Tek cihaz politikası (concurrent session engeli)
- CORS sertleştirme

## 📊 Performans

- Redis Cache-Aside pattern (15+ read metod cache'li)
- DB Index optimizasyonu (tüm sık sorgulanan alanlar)
- Response Compression (Brotli + Gzip)
- BunnyCDN video streaming (HLS adaptive bitrate)

## 📖 Dokümantasyon

- [Mimari Tasarım](docs/ARCHITECTURE.md)
- [Deploy Rehberi](docs/DEPLOYMENT.md)
- [Ortam Değişkenleri](docs/ENVIRONMENT.md)
- Swagger UI: `/swagger` (Development modda)

## 🏥 Health Check

| Endpoint | Amaç |
|----------|------|
| `GET /health` | Tam diagnostik (DB + Redis + BBB + Sistem) |
| `GET /health/live` | Liveness probe |
| `GET /health/ready` | Readiness probe (DB + Redis) |
