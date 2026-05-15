#!/bin/bash
# ============================================
# MURO LMS — Güvenli ve Temiz Güncelleme Scripti
# ============================================
# Kullanım: 
# muro_guncelleme.zip dosyasını sunucuya yükleyin.
# Ardından terminalde veya Plesk Cron Job'da şu komutu çalıştırın:
# cd /var/www/vhosts/on7medya.com/demo.muro.on7medya.com/muro && bash scripts/deploy_update.sh

set -e

echo "🚀 MURO LMS Güvenli Güncelleme Başlıyor..."
echo "========================================="

# 1. Zip dosyasının varlığını kontrol et
if [ ! -f "muro_guncelleme.zip" ]; then
    echo "❌ HATA: muro_guncelleme.zip dosyası bulunamadı!"
    echo "Lütfen bilgisayarınızdaki muro_guncelleme.zip dosyasını sunucudaki bu klasöre yükleyin."
    exit 1
fi

echo "🧹 1/5 — Eski kalıntı dosyalar siliniyor (Ghost File Koruması)..."
# Plesk Extract'ın üzerine yazma ve eski dosyaları bırakma huyunu engelliyoruz.
rm -rf src frontend

echo "📦 2/5 — Yeni kodlar arşivden çıkarılıyor..."
unzip -q -o muro_guncelleme.zip
# Çıkarma işlemi bitince diskte yer kaplamaması için zip dosyasını otomatik siliyoruz.
rm muro_guncelleme.zip

echo "🧽 3/5 — Docker eski imaj ve cache temizliği yapılıyor (Disk Koruması)..."
# Şişen diski rahatlatıp derlemeye bol yer açıyoruz.
docker system prune -a -f --volumes
docker builder prune -a -f

echo "🏗️  4/5 — Yeni kodlar RAM Dostu Modda derleniyor..."
# --parallel sildik, sunucu boğulmadan sırayla derleyecek.
docker compose -f docker-compose.prod.yml build

echo "🌐 5/5 — Servisler yeni kodlarla ayağa kaldırılıyor..."
docker compose -f docker-compose.prod.yml up -d

echo "========================================="
echo "✅ MURO LMS GÜNCELLEMESİ KUSURSUZ TAMAMLANDI!"
echo "========================================="
