with open(r'c:\Users\Rüstem\.gemini\antigravity\scratch\muro-v2\frontend\admin\src\app\dashboard\groups\page.tsx', 'r', encoding='utf-8') as f:
    lines = f.read().split('\n')

lines[769] = '                            <p className="text-[10px] text-[#A0AEC0]">{!formType ? <span className="text-red-400">Eğitim modeli seçin</span> : !formName.trim() ? "Grup adı gerekli" : "✅ Hazır"}</p>'
lines[771] = '                                <button onClick={() => setFormOpen(false)} className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931]">İptal</button>'
lines[775] = '                                    {editGroup ? "Kaydet" : "Oluştur"}'

with open(r'c:\Users\Rüstem\.gemini\antigravity\scratch\muro-v2\frontend\admin\src\app\dashboard\groups\page.tsx', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))
