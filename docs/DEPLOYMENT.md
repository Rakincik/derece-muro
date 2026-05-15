# Deploy Rehberi

## Gereksinimler (Sunucu)

- Ubuntu 22.04+ veya Debian 12+
- Docker 24+ ve Docker Compose v2
- En az 4 GB RAM, 2 CPU core
- PostgreSQL 15+ (ayrı container veya sunucu)
- Redis 7+ (ayrı container veya sunucu)

## Docker ile Deploy

### 1. Proje Build

```bash
# Production image oluştur
docker build -t muro-api:latest -f Dockerfile .
```

### 2. Docker Compose

```yaml
# docker-compose.production.yml
services:
  api:
    image: muro-api:latest
    restart: unless-stopped
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=muro;Username=muro_user;Password=CHANGE_ME
      - ConnectionStrings__Redis=redis:6379,password=CHANGE_ME
      - Jwt__Secret=CHANGE_ME_MIN_32_CHARS
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health/live"]
      interval: 30s
      timeout: 5s
      retries: 3
    deploy:
      resources:
        limits:
          memory: 2G
    depends_on:
      - db
      - redis

  db:
    image: postgres:15
    restart: unless-stopped
    environment:
      POSTGRES_DB: muro
      POSTGRES_USER: muro_user
      POSTGRES_PASSWORD: CHANGE_ME
    volumes:
      - pgdata:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    restart: unless-stopped
    command: redis-server --requirepass CHANGE_ME
    volumes:
      - redisdata:/data

volumes:
  pgdata:
  redisdata:
```

### 3. Başlatma

```bash
docker-compose -f docker-compose.production.yml up -d
```

### 4. Migration

```bash
# Yeni migration'ları uygula
docker exec muro-api dotnet ef database update
```

## Manuel Deploy (VEP Üzerinden)

1. VEP kontrol panelinden sunucuya SSH bağlantısı
2. `git pull origin main`
3. `docker-compose build --no-cache api`
4. `docker-compose up -d api`
5. Health check: `curl http://localhost:5000/health/ready`

## Deploy Sonrası Kontrol Listesi

- [ ] `GET /health` — tüm servisler `healthy`
- [ ] `GET /health/ready` — `Ready` dönüyor
- [ ] Swagger UI erişilebilir (sadece Development'ta)
- [ ] Frontend login çalışıyor
- [ ] Redis bağlantısı aktif (cache istatistikleri)
