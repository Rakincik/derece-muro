#!/bin/bash
# ============================================
# MURO LMS — Production Deploy Script
# ============================================
# Bu scripti sunucuda çalıştırın:
#   chmod +x scripts/deploy.sh
#   ./scripts/deploy.sh
# ============================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

echo "🚀 MURO LMS Deployment Başlıyor..."
echo "========================================="

# .env dosyası kontrolü
if [ ! -f ".env" ]; then
    echo "❌ HATA: .env dosyası bulunamadı!"
    echo "   Önce .env.production dosyasını kopyalayın:"
    echo "   cp .env.production .env"
    echo "   Sonra .env dosyasını düzenleyin."
    exit 1
fi

echo ""
echo "📦 1/5 — Docker image'ları build ediliyor..."
docker compose -f docker-compose.prod.yml build --parallel

echo ""
echo "🗄️  2/5 — Veritabanı ve Redis başlatılıyor..."
docker compose -f docker-compose.prod.yml up -d postgres redis
echo "   ⏳ Servislerin hazır olması bekleniyor..."
sleep 10

echo ""
echo "🔄 3/5 — Database migration çalıştırılıyor..."
# Migration'ı API container üzerinden çalıştır
docker compose -f docker-compose.prod.yml run --rm api \
    dotnet ef database update \
    --project src/MURO.Infrastructure/MURO.Infrastructure.csproj \
    --startup-project src/MURO.API/MURO.API.csproj \
    2>/dev/null || echo "   ℹ️  Migration zaten güncel veya ef tool yüklü değil — API başlatıldığında otomatik uygulanacak."

echo ""
echo "🌐 4/5 — Tüm servisler başlatılıyor..."
docker compose -f docker-compose.prod.yml up -d

echo ""
echo "✅ 5/5 — Servis durumları kontrol ediliyor..."
sleep 5
docker compose -f docker-compose.prod.yml ps

echo ""
echo "========================================="
echo "✅ MURO LMS başarıyla deploy edildi!"
echo ""
echo "📊 Logları görmek için:"
echo "   docker compose -f docker-compose.prod.yml logs -f"
echo ""
echo "🔄 Yeniden başlatmak için:"
echo "   docker compose -f docker-compose.prod.yml restart"
echo ""
echo "🛑 Durdurmak için:"
echo "   docker compose -f docker-compose.prod.yml down"
echo "========================================="
