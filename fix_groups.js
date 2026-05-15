const fs = require('fs');
const path = 'frontend/admin/src/app/dashboard/groups/page.tsx';
let text = fs.readFileSync(path, 'utf8');

const replacements = {
    'Ò  Ÿrenci': 'Öğrenci',
    'gruplar ±n ±': 'gruplarını',
    'yÒ¶netin': 'yönetin',
    'Ò ye': 'Üye',
    'Atamas ±': 'Ataması',
    'Bo&Ÿ': 'Boş',
    'henÒ¼z': 'henüz',
    'A ŸaÒ§ta': 'Ağaçta',
    'âš ï¸ ': '⚠️',
    'i&Ÿaretlendi': 'işaretlendi',
    'SonuÒ§': 'Sonuç',
    'bulunamad ±': 'bulunamadı',
    'AÒ§ ±klama': 'Açıklama',
    'Ta&Ÿ ±': 'Taşı',
    'Ò st': 'Üst',
    'De Ÿi&Ÿtir': 'Değiştir',
    'Olu&Ÿtur': 'Oluştur',
    'E Ÿitim': 'Eğitim',
    'SeÒ§in': 'Seçin',
    'Canl ±': 'Canlı',
    'S ±nav': 'Sınav',
    'Ad ±': 'Adı',
    'Ò rn:': 'Örn:',
    'S ±n ±f ±': 'Sınıfı',
    'Ò⬡al ±&Ÿma': 'Çalışma',
    'KÒ¶k': 'Kök',
    'Ba Ÿ ±ms ±z': 'Bağımsız',
    'Ò nizleme': 'Önizleme',
    ' °puÒ§lar ±': 'İpuçları',
    'tan ±man ±z ±': 'tanımanızı',
    'sa Ÿlar': 'sağlar',
    'hiyerar&Ÿik': 'hiyerarşik',
    'yap ±': 'yapı',
    'Ò¶ Ÿrenciler': 'öğrenciler',
    'seÒ§in': 'seçin',
    'â S Haz ±r': '✅ Hazır',
    ' °ptal': 'İptal',
    'xŸ\"¾ Kaydet': 'Kaydet',
    'â ¨ Olu&Ÿtur': 'Oluştur',
    'kullan ±c ±': 'kullanıcı',
    'Ò§ ±kar ±l ±p': 'çıkarılıp',
    'Eri&Ÿim': 'Erişim',
    'Yay ±na': 'Yayına',
    'Ar&Ÿivine': 'Arşivine',
    'Syeleri': 'Üyeleri',
    'Taxı': 'Taşı',
    'baxlandıxı': 'bağlandığı',
    'dexixtirin': 'değiştirin',
    'Baxımsız': 'Bağımsız',
    'TSM': 'TÜM',
    'atanmıx': 'atanmış',
    'xekilde': 'şekilde',
    'ixlem': 'işlem',
    'istedi Ÿinize': 'istediğinize',
    'DÒ¼zenle': 'Düzenle',
    'Kullan ±c ±': 'Kullanıcı',
    'xŸŽ¥': '🎥',
    'xŸS ': '📴',
    'xŸ ⬢ï¸ ': '🏕️',
    'xŸS ': '📝',
    'xŸ⬝⬞': '🔄',
    'xŸŽ¯': '🎯',
    'â⬠': '↳',
    'â ⬝': '--',
    'xŸ\"¡': '👉',
    'xŸŽ¨': '🎨',
    'xŸSa': '📂',
    'xŸ¥': '✨',
    ' °mha Et': 'İmha Et',
    'aÒ§ ±klama': 'açıklama',
    '-Yrenci': 'Öğrenci',
    'gruplarn': 'gruplarını',
    'ynetin': 'yönetin',
    '-YE': 'ÜYE',
    'ATAMAS-': 'ATAMASI',
    'BO&Y': 'BOŞ',
    '1 ¼ye': '1 üye',
    '0 ders': '0 ders',
    'Canl-': 'Canlı'
};

// Also replace standard broken variations from ISO to UTF-8
text = text.replace(/-Y/g, 'Ö');
text = text.replace(/¼/g, 'ü');
text = text.replace(/n/g, 'ını');
text = text.replace(/-/g, 'ı');
text = text.replace(/&Y/g, 'Ş');
text = text.replace(/¶/g, 'ö');
text = text.replace(/§/g, 'ç');
text = text.replace(/x/g, 'ş');
text = text.replace(//g, 'ğ');

for (const [bad, good] of Object.entries(replacements)) {
    text = text.split(bad).join(good);
}

fs.writeFileSync(path, text, 'utf8');
console.log('Fixed groups page with nodejs!');
