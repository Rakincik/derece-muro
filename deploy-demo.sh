#!/bin/bash
# ============================================
# MURO LMS Demo — Full Deploy Script
# ============================================
# Sunucu: 185.184.25.150
# Subdomains: 3u.muro.click, 3u-ad.muro.click, 3u-ap.muro.click
# ============================================
# Kullanım:
#   cd /opt/muro-demo
#   chmod +x deploy-demo.sh
#   ./deploy-demo.sh
# ============================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo ""
echo "╔═══════════════════════════════════════════════════╗"
echo "║     MURO LMS Demo — Sıfırdan Deploy 🚀           ║"
echo "║     3u.muro.click | 3u-ad | 3u-ap                ║"
echo "╚═══════════════════════════════════════════════════╝"
echo ""

# ── 0. Önceki container'ları temizle ──
echo "🧹 Eski container'lar temizleniyor..."
docker compose -f docker-compose.demo.yml down -v 2>/dev/null || true
docker system prune -f 2>/dev/null || true
echo "✅ Temizlik tamamlandı"
echo ""

# ── 1. .env kontrolü ──
if [ ! -f ".env" ]; then
    if [ -f ".env.3u" ]; then
        echo "📋 .env.3u → .env kopyalanıyor..."
        cp .env.3u .env
    else
        echo "❌ HATA: .env dosyası bulunamadı!"
        echo "   .env.3u dosyasını düzenleyip .env olarak kopyalayın."
        exit 1
    fi
fi
echo "✅ .env dosyası mevcut"
echo ""

# ── 2. Swap kontrolü ──
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
    echo "✅ Swap mevcut: $(free -h | grep Swap | awk '{print $2}')"
fi
echo ""

# ── 3. Storage dizinleri ──
echo "📁 Storage dizinleri oluşturuluyor..."
mkdir -p /opt/muro-demo-data/{hls,podcasts,uploads,logs}
echo "✅ Storage dizinleri hazır"
echo ""

# ── 4. Docker build ──
echo "📦 Docker image'ları build ediliyor (bu 5-15 dk sürebilir)..."
echo "   ⏳ Lütfen bekleyin..."
docker compose -f docker-compose.demo.yml build --parallel 2>&1 | tail -20
echo ""
echo "✅ Build tamamlandı"
echo ""

# ── 5. Veritabanı ve Redis başlat ──
echo "🗄️  PostgreSQL ve Redis başlatılıyor..."
docker compose -f docker-compose.demo.yml up -d postgres redis
echo "   ⏳ Servislerin hazır olması bekleniyor..."
sleep 15

# Health check loop
for i in {1..10}; do
    if docker compose -f docker-compose.demo.yml exec -T postgres pg_isready -U muro_user -d muro_demo > /dev/null 2>&1; then
        echo "✅ PostgreSQL hazır"
        break
    fi
    echo "   ⏳ PostgreSQL bekleniyor... ($i/10)"
    sleep 5
done

if docker compose -f docker-compose.demo.yml exec -T redis redis-cli ping > /dev/null 2>&1; then
    echo "✅ Redis hazır"
fi
echo ""

# ── 6. Tüm servisler ──
echo "🌐 Tüm servisler başlatılıyor..."
docker compose -f docker-compose.demo.yml up -d
echo ""

# ── 7. Nginx config ──
echo "🔧 Nginx yapılandırılıyor..."
cp nginx/3u-demo.conf /etc/nginx/sites-available/3u-demo.conf 2>/dev/null || \
cp nginx/3u-demo.conf /etc/nginx/conf.d/3u-demo.conf

# sites-enabled varsa symlink oluştur
if [ -d "/etc/nginx/sites-enabled" ]; then
    ln -sf /etc/nginx/sites-available/3u-demo.conf /etc/nginx/sites-enabled/3u-demo.conf
fi

# Nginx test
if nginx -t 2>&1; then
    systemctl reload nginx
    echo "✅ Nginx yapılandırması yüklendi ve reload edildi"
else
    echo "❌ Nginx config hatası! Kontrol edin:"
    echo "   nginx -t"
    echo "   Devam ediliyor..."
fi
echo ""

# ── 8. Servislerin başlaması bekleniyor ──
echo "⏳ Servisler başlatılıyor, 30 saniye bekleniyor..."
sleep 30
echo ""

# ── 9. Durum kontrolü ──
echo "📊 Servis Durumları:"
echo "═══════════════════════════════════════════════"
docker compose -f docker-compose.demo.yml ps
echo ""

# ── 10. Health check ──
echo "🏥 Health Check:"
echo "───────────────────────────────────────────────"

# API
if curl -sf http://127.0.0.1:5292/api/health > /dev/null 2>&1; then
    echo "   ✅ API (5292): Sağlıklı"
else
    echo "   ⏳ API (5292): Henüz başlatılıyor..."
    echo "      → Log: docker compose -f docker-compose.demo.yml logs api"
fi

# Admin
if curl -sf http://127.0.0.1:3001 > /dev/null 2>&1; then
    echo "   ✅ Admin (3001): Sağlıklı"
else
    echo "   ⏳ Admin (3001): Henüz başlatılıyor..."
fi

# Student
if curl -sf http://127.0.0.1:3002 > /dev/null 2>&1; then
    echo "   ✅ Student (3002): Sağlıklı"
else
    echo "   ⏳ Student (3002): Henüz başlatılıyor..."
fi

echo ""
echo "╔═══════════════════════════════════════════════════╗"
echo "║  🎉 MURO Demo Deploy Tamamlandı!                 ║"
echo "║                                                   ║"
echo "║  🌐 Student:  https://3u.muro.click               ║"
echo "║  🔧 Admin:    https://3u-ad.muro.click            ║"
echo "║  📡 API:      https://3u-ap.muro.click/api/health ║"
echo "╚═══════════════════════════════════════════════════╝"
echo ""
echo "📊 Logları görmek için:"
echo "   docker compose -f docker-compose.demo.yml logs -f"
echo ""
echo "📊 Sadece API logları:"
echo "   docker compose -f docker-compose.demo.yml logs -f api"
echo ""
