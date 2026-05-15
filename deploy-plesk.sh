#!/bin/bash
# ============================================
# MURO LMS — Plesk Server Full Deploy Script
# ============================================
# Kullanım:
#   cd /opt/muro-v2
#   chmod +x deploy-plesk.sh
#   ./deploy-plesk.sh
# ============================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo ""
echo "╔═══════════════════════════════════════════════════╗"
echo "║       MURO LMS — Plesk Deployment 🚀             ║"
echo "╚═══════════════════════════════════════════════════╝"
echo ""

# ── 1. .env kontrolü ──
if [ ! -f ".env" ]; then
    if [ -f ".env.deploy" ]; then
        echo "📋 .env.deploy → .env kopyalanıyor..."
        cp .env.deploy .env
    else
        echo "❌ HATA: .env dosyası bulunamadı!"
        echo "   .env.deploy dosyasını düzenleyip .env olarak kopyalayın."
        exit 1
    fi
fi

echo "✅ .env dosyası mevcut"
echo ""

# ── 2. Swap kontrolü (RAM yetersiz olabilir) ──
SWAP=$(free | grep Swap | awk '{print $2}')
if [ "$SWAP" -eq 0 ]; then
    echo "⚠️  Swap alanı yok. 2GB swap oluşturuluyor..."
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
    echo "✅ 2GB swap oluşturuldu"
else
    echo "✅ Swap mevcut"
fi
echo ""

# ── 3. Docker build ──
echo "📦 Docker image'ları build ediliyor (bu 5-10 dk sürebilir)..."
docker compose -f docker-compose.prod.yml build --parallel
echo "✅ Build tamamlandı"
echo ""

# ── 4. Veritabanı ve Redis başlat ──
echo "🗄️  PostgreSQL ve Redis başlatılıyor..."
docker compose -f docker-compose.prod.yml up -d postgres redis
echo "   ⏳ Servislerin hazır olması bekleniyor..."
sleep 15
echo "✅ Veritabanı ve Redis hazır"
echo ""

# ── 5. Tüm servisler ──
echo "🌐 Tüm servisler başlatılıyor..."
docker compose -f docker-compose.prod.yml up -d
echo ""

# ── 6. Durum kontrolü ──
echo "⏳ Servisler başlatılıyor, 10 saniye bekleniyor..."
sleep 10
echo ""
echo "📊 Servis Durumları:"
echo "═══════════════════════════════════════════════"
docker compose -f docker-compose.prod.yml ps
echo ""

# ── 7. Health check ──
echo "🏥 Health Check:"
if curl -sf http://127.0.0.1:5292/health > /dev/null 2>&1; then
    echo "   ✅ API: Sağlıklı"
else
    echo "   ⏳ API henüz başlatılıyor... (30 saniye bekleyin, tekrar deneyin)"
fi

if curl -sf http://127.0.0.1:3001 > /dev/null 2>&1; then
    echo "   ✅ Admin Frontend: Sağlıklı"
else
    echo "   ⏳ Admin Frontend başlatılıyor..."
fi

if curl -sf http://127.0.0.1:3002 > /dev/null 2>&1; then
    echo "   ✅ Student Frontend: Sağlıklı"
else
    echo "   ⏳ Student Frontend başlatılıyor..."
fi

echo ""
echo "╔═══════════════════════════════════════════════════╗"
echo "║  🎉 MURO LMS Deploy Tamamlandı!                  ║"
echo "║                                                   ║"
echo "║  Sonraki Adım: Plesk Nginx Proxy Yapılandırması   ║"
echo "║  api-muro.on7medya.com  → 127.0.0.1:5292          ║"
echo "║  admin-muro.on7medya.com → 127.0.0.1:3001         ║"
echo "║  ogrenci-muro.on7medya.com → 127.0.0.1:3002       ║"
echo "╚═══════════════════════════════════════════════════╝"
echo ""
echo "📊 Logları görmek için:"
echo "   docker compose -f docker-compose.prod.yml logs -f"
echo ""
